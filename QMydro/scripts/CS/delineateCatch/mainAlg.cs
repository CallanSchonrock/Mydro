using System;
using System.Collections.Generic;
using System.Diagnostics;
using MathNet.Numerics;
using MathNet.Numerics.Statistics;


namespace mainAlg
{
    class Subcatchment
    {
        public int id;
        public List<Subcatchment> upstreamCatchments = new List<Subcatchment>() { };
        public int dsCatchment = -1;
        public int numUS = 0;
        public bool rooted = false;
        public Subcatchment(int idToAssign)
        {
            id = idToAssign;
        }
    }
    class mainAlgorithm
    {
        // static Dictionary<(int, int), int> dd = new Dictionary<(int, int), int>() { { (-1, -1), 1 }, { (-1, 0), 2 }, { (-1, 1) , 3}, { (0, -1), 4 }, { (0, 1), 5 }, { (1, -1), 6 }, { (1, 0), 7 }, { (1, 1), 8 } };
        static int[,] d8 = new int[,]
        {
        {1, 2, 3 },
        {4, 0, 5 },
        {6, 7, 8 }
        };

        static Dictionary<int, (int, int)> reverseD8 = new Dictionary<int, (int, int)>() { { 1, (-1, -1) }, { 2, (-1, 0) }, { 3, (-1, 1) }, { 4, (0, -1) }, { 5, (0, 1) }, { 6, (1, -1) }, { 7, (1, 0) }, { 8, (1, 1) } };

        static List<(int, int)> dEightFlowAlg(float[,] elev, int numRows, int numCols, float noData_val)
        {
            List<(int, int)> outletList = new List<(int, int)>();

            for (int i = 0; i < numRows; i++)
            {
                for (int j = 0; j < numCols; j++)
                {

                    if (elev[i, j] == noData_val)
                    {
                        continue;
                    }

                    float minElev = 99999;
                    bool noDataNeighbour = false;

                    for (int di = -1; di <= 1; di++)
                    {
                        int ni = i + di;
                        if (ni < 0 || ni >= numRows) { noDataNeighbour = true; continue; }
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            if (di == 0 && dj == 0) { continue; }
                            int nj = j + dj;
                            if (nj < 0 || nj >= numCols) { noDataNeighbour = true; continue; }
                            if (elev[ni, nj] == noData_val) { noDataNeighbour = true; continue; }
                            if (elev[ni, nj] < minElev)
                            {
                                minElev = elev[ni, nj];
                            }
                        }
                    }

                    if (noDataNeighbour)
                    {
                        if (elev[i, j] <= minElev)
                        {
                            outletList.Add((i, j));
                        }
                    }

                }
            }
            return outletList;
        }

        static void Shuffle<T>(List<T> list)
        {
            Random rand = new Random();
            int n = list.Count;

            for (int i = n - 1; i > 0; i--)
            {
                // Pick a random index from 0 to i
                int randIndex = rand.Next(0, i + 1);

                // Swap list[i] with the element at randIndex
                T temp = list[i];
                list[i] = list[randIndex];
                list[randIndex] = temp;
            }
        }

        public static (int[,], int[,], int[,], List<float>, List<float>) processingAlg(float[,] elev, float noData_val, List<List<(int, int)>> outletCells, float dx, float dy, float targetCatchSize)
        {
            int numRows = elev.GetLength(0);
            int numCols = elev.GetLength(1);
            int[,] catchments = new int[numRows, numCols];
            int[,] outletCatchments = new int[numRows, numCols];
            int[,] drainageMap = new int[numRows, numCols];
            int[,] cellNeighbours = new int[numRows, numCols];
            int[,] accumulation = new int[numRows, numCols];
            int[,] subcatchments = new int[numRows, numCols];
            int[,] channel = new int[numRows, numCols];
            Console.WriteLine($"Target Subcatchment Cells: {targetCatchSize}");
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            // Iterate over each cell in the elevation array
            List<(int, int)> outletsList = dEightFlowAlg(elev, numRows, numCols, noData_val);
            stopwatch.Stop();
            Console.WriteLine($"Raster Outflows: {outletsList.Count}, Time: {stopwatch.ElapsedMilliseconds}");

            PriorityQueue<(int, int), float> lowestCells = new PriorityQueue<(int, int), float>();
            PriorityQueue<(int, int), float> highestCells = new PriorityQueue<(int, int), float>();
            PriorityQueue<(int, int), float> topOfCatchmentCells = new PriorityQueue<(int, int), float>();
            PriorityQueue<(int, int), float> subCells = new PriorityQueue<(int, int), float>();

            int catchmentID = 1;
            foreach ((int, int) outletPixel in outletsList)
            {
                outletCatchments[outletPixel.Item1, outletPixel.Item2] = catchmentID;
                lowestCells.Enqueue(outletPixel, elev[outletPixel.Item1, outletPixel.Item2]);
                catchmentID++;
            }
            Console.WriteLine($"Outlet Pixels: {lowestCells.Count}");

            stopwatch.Start();
            catchmentID = 1;
            Dictionary<int, Subcatchment> subcatchmentsInfo = new Dictionary<int, Subcatchment>();
            foreach (List<(int, int)> userOutlet in outletCells)
            {
                int lastx = userOutlet[0].Item1;
                int lasty = userOutlet[0].Item2;
                foreach ((int x, int y) in userOutlet)
                {
                    catchments[x, y] = catchmentID;
                    if (lastx != x && lasty != y)
                    {
                        catchments[lastx, y] = catchmentID;
                        catchments[x, lasty] = catchmentID;
                    }
                }
                subcatchmentsInfo.Add(catchmentID, new Subcatchment(catchmentID));
                catchmentID++;
            }
            List<(int, int)> catchList = new List<(int, int)>();
            while (lowestCells.Count > 0)
            {
                int neighbouringCells = 0;
                (int x, int y) = lowestCells.Dequeue();

                int xOffset = x - 1;
                int yOffset = y - 1;
                int catchment = catchments[x, y];
                int outletCatchment = outletCatchments[x, y];
                List<(int, int, float)> cellsToEnqueue = new List<(int, int, float)>();
                for (int i = x - 1; i <= x + 1; i++)
                {
                    if (i < 0 || i > numRows - 1) continue;
                    for (int j = y - 1; j <= y + 1; j++)
                    {
                        if (j < 0 || j > numCols - 1) { continue; }
                        if (outletCatchments[i, j] != default) { continue; }
                        float elevation = elev[i, j];
                        if (elevation == noData_val) { continue; }

                        outletCatchments[i, j] = outletCatchment;
                        if (catchment != default) { catchments[i, j] = catchment; }
                        // drainageMap[i, j] = dd[(i - x, j - y)]
                        drainageMap[i, j] = d8[i - xOffset, j - yOffset];
                        cellsToEnqueue.Add((i, j, elevation));
                        neighbouringCells += 1;
                    }
                }
                Shuffle(cellsToEnqueue);
                foreach ((int, int, float) cell in cellsToEnqueue)
                {
                    lowestCells.Enqueue((cell.Item1, cell.Item2), cell.Item3);
                }

                cellNeighbours[x, y] = neighbouringCells;
                if (neighbouringCells == 0)
                {
                    highestCells.Enqueue((x, y), elev[x, y]);
                    topOfCatchmentCells.Enqueue((x, y), elev[x, y]);
                    catchList.Add((x, y));
                }

            }
            Shuffle(catchList);
            int[,] catchCellNeighbours = (int[,])cellNeighbours.Clone();
            stopwatch.Stop();
            Console.WriteLine($"Watersheds Defined. Time: {stopwatch.ElapsedMilliseconds}");


            stopwatch.Start();
            while (highestCells.Count > 0)
            {
                (int x, int y) = highestCells.Dequeue();
                accumulation[x, y] += 1;
                for (int i = x - 1; i <= x + 1; i++)
                {
                    if (i < 0 || i > numRows - 1) { continue; }
                    for (int j = y - 1; j <= y + 1; j++)
                    {
                        if (i == x && j == y) { continue; }
                        if (j < 0 || j > numCols - 1) { continue; }
                        if (elev[i, j] == noData_val) { continue; }

                        //if (dd[(x - i, y - j)] == drainageMap[x, y])
                        if (d8[x - i + 1, y - j + 1] == drainageMap[x, y])
                        {
                            accumulation[i, j] += accumulation[x, y];
                            cellNeighbours[i, j] -= 1;
                            if (cellNeighbours[i, j] == 0)
                            {
                                highestCells.Enqueue((i, j), elev[i, j]);
                            }
                        }
                    }
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"Accumulation Calculated. Time: {stopwatch.ElapsedMilliseconds}");


            List<int> catchAccs = new List<int>();
            List<int> catchMaxAccs = new List<int>();
            List<(double, double)> catchHydraulicParameters = new List<(double, double)>();
            stopwatch.Start();
            catchmentID = 1;
            foreach (List<(int, int)> userOutlet in outletCells)
            {
                int lastx = userOutlet[0].Item1;
                int lasty = userOutlet[0].Item2;
                catchAccs.Add(0);
                catchMaxAccs.Add(0);
                List<(int, int, int, float)> subCatOutletCells = new List<(int, int, int, float)>();
                List<int> indexesToSkip = new List<int>();
                foreach ((int x, int y) in userOutlet)
                {
                    subCatOutletCells.Add((x, y, accumulation[x, y], elev[x, y]));
                    if (lastx != x && lasty != y)
                    {
                        subCatOutletCells.Add((lastx, y, accumulation[lastx, y], elev[lastx, y]));
                        indexesToSkip.Add(subCatOutletCells.Count - 1);
                        subCatOutletCells.Add((x, lasty, accumulation[x, lasty], elev[x, lasty]));
                        indexesToSkip.Add(subCatOutletCells.Count - 1);
                    }
                    lastx = x;
                    lasty = y;
                }

                List<int> xsX = subCatOutletCells.Select(tuple => tuple.Item1).ToList();
                List<int> xsY = subCatOutletCells.Select(tuple => tuple.Item2).ToList();
                List<float> xsElev = subCatOutletCells.Select(tuple => tuple.Item4).ToList();

                float xsMin = xsElev.Min();
                int xsMinIndex = xsElev.IndexOf(xsMin);

                float leftMaxElev = xsElev.Take(xsMinIndex + 1).Max();
                float rightMaxElev = xsElev.Skip(Math.Max(0,xsElev.Count - (xsMinIndex+1))).Max();

                float minMaxElev = Math.Min(leftMaxElev, rightMaxElev);

                float stepSize = (float) Math.Min((minMaxElev - xsMin) / 10, 1.0);
                double slope = 0; double yIntercept = 0;
                if (minMaxElev - xsMin > 0.5 && xsX.Count >= 5 && dx + dy <= 20)
                {
                    List<double> conveyanceAreaLN = new List<double>();
                    List<double> hydraulicR = new List<double>();
                    for (int i = 2; i <= 10; i++)
                    {
                        float waterLevel = xsMin + i * stepSize;
                        float wettedPerimeter = 0;
                        float conveyanceArea = 0;
                        int lastIndex = 0;
                        for (int indexOffset = 0; indexOffset < xsX.Count(); indexOffset++)
                        {
                            if (indexesToSkip.Contains(indexOffset)) { continue; }
                            if (xsElev[indexOffset] >= waterLevel) { continue; }

                            float distance = (float) Math.Sqrt(Math.Pow((xsX[indexOffset] - xsX[lastIndex]) * dx, 2) + Math.Pow((xsY[indexOffset] - xsY[lastIndex]) * dy, 2));
                            wettedPerimeter += (float) Math.Sqrt(Math.Pow(distance,2) + Math.Pow(xsElev[indexOffset] - xsElev[lastIndex],2));
                            conveyanceArea += distance * (waterLevel - xsElev[indexOffset]);
                            lastIndex = indexOffset;
                        }
                        if (conveyanceArea == 0 || wettedPerimeter == 0) { continue; }
                        
                        conveyanceAreaLN.Add(Math.Log(conveyanceArea));
                        hydraulicR.Add(Math.Pow(conveyanceArea / wettedPerimeter, (2.0 / 3.0)));
                    }

                    try
                    {
                        // Calculate the mean of x and y values
                        double meanX = conveyanceAreaLN.Average();
                        double meanY = hydraulicR.Average();

                        // Calculate the slope (m)
                        double numerator = 0;
                        double denominator = 0;

                        for (int i = 0; i < conveyanceAreaLN.Count; i++)
                        {
                            numerator += (conveyanceAreaLN[i] - meanX) * (hydraulicR[i] - meanY);
                            denominator += Math.Pow(conveyanceAreaLN[i] - meanX, 2);
                        }

                        slope = numerator / denominator;

                        // Calculate the y-intercept (b)
                        yIntercept = meanY - slope * meanX;
                    }
                    catch
                    {

                    }
                }
                catchHydraulicParameters.Add((slope, yIntercept));

                subCatOutletCells = subCatOutletCells.OrderByDescending(tuple => tuple.Item3).ToList();
                subCatOutletCells = subCatOutletCells.Where(tuple => tuple.Item3 > 250).ToList();
                foreach((int x, int y, int acc, float cellElev) in subCatOutletCells)
                {
                    subCells.Enqueue((x, y), accumulation[x, y]);
                    subcatchments[x, y] = catchmentID;
                    catchAccs[catchmentID - 1] += 1;
                    catchMaxAccs[catchmentID - 1] = Math.Max(catchMaxAccs[catchmentID - 1], accumulation[x, y]);
                }
                catchmentID++;
            }


            while (subCells.Count > 0)
            {
                (int x, int y) = subCells.Dequeue();
                int thisSubCat = subcatchments[x, y];
                int thisAcc = accumulation[x, y];
                for (int i = x - 1; i <= x + 1; i++)
                {
                    if (i < 0 || i > numRows - 1) continue;
                    for (int j = y - 1; j <= y + 1; j++)
                    {
                        if (i == x && j == y) { continue; }
                        if (j < 0 || j > numCols - 1) { continue; }
                        if (elev[i, j] == noData_val) { continue; }

                        //if (dd[(i - x, j - y)] == drainageMap[i, j])
                        if (d8[i - x + 1, j - y + 1] == drainageMap[i, j])
                        {
                            int nextSubCat = subcatchments[i, j];
                            if (nextSubCat == thisSubCat)
                            {
                                continue;
                            }
                            if (nextSubCat != default) // VECFILE ROUTING
                            {
                                if (!subcatchmentsInfo[thisSubCat].upstreamCatchments.Contains(subcatchmentsInfo[nextSubCat]) && !subcatchmentsInfo[nextSubCat].upstreamCatchments.Contains(subcatchmentsInfo[thisSubCat]) && catchMaxAccs[thisSubCat - 1] > catchMaxAccs[nextSubCat - 1])
                                {
                                    subcatchmentsInfo[thisSubCat].upstreamCatchments.Add(subcatchmentsInfo[nextSubCat]);
                                    subcatchmentsInfo[nextSubCat].dsCatchment = thisSubCat;
                                }
                                continue;
                            }
                            subCells.Enqueue((i, j), accumulation[i, j]);
                            if (targetCatchSize > 0 && catchAccs[thisSubCat - 1] > targetCatchSize * 0.75 && accumulation[i, j] > targetCatchSize * 0.5 && thisAcc - accumulation[i,j] > targetCatchSize * 0.25)
                            {
                                
                                subcatchmentsInfo.Add(catchmentID, new Subcatchment(catchmentID));
                                catchAccs.Add(1);
                                catchMaxAccs.Add(accumulation[i, j]);
                                subcatchments[i, j] = catchmentID;
                                catchHydraulicParameters.Add((0, 0));
                                outletCells.Add(new List<(int, int)> { (i, j) });
                                subcatchmentsInfo[thisSubCat].upstreamCatchments.Add(subcatchmentsInfo[catchmentID]);
                                subcatchmentsInfo[catchmentID].dsCatchment = thisSubCat;
                                catchmentID++;
                            }
                            else
                            {
                                subcatchments[i, j] = thisSubCat;
                                catchAccs[thisSubCat - 1] += 1;
                            }
                        }
                    }
                }
            }
            
            stopwatch.Stop();
            Console.WriteLine($"Catchments Calculated. Time: {stopwatch.ElapsedMilliseconds}");


            
            stopwatch.Start();
            List<float> channelSlopes = new List<float>();
            List<float> channelLengths = new List<float>();

            List<float> channelCatchSlopes = new List<float>();
            List<float> channelCatchLengths = new List<float>();
            for (int i = 1; i < catchmentID; i++)
            {
                channelSlopes.Add(0);
                channelLengths.Add(0);
                channelCatchSlopes.Add(0);
                channelCatchLengths.Add(0);
            }

            foreach (List<(int, int)> userOutlet in outletCells)
            {
                (int, (int, int)) mostAccumulation = (-1, (0, 0)); // (Accumulation, (x,y))
                int lastx = userOutlet[0].Item1;
                int lasty = userOutlet[0].Item2;
                
                foreach ((int ix, int iy) in userOutlet)
                {
                    if (accumulation[ix, iy] > mostAccumulation.Item1)
                    {
                        mostAccumulation = (accumulation[ix, iy], (ix, iy));
                    }
                    if (lastx != ix && lasty != iy)
                    {
                        if (accumulation[lastx, iy] > mostAccumulation.Item1)
                        {
                            mostAccumulation = (accumulation[lastx, iy], (lastx, iy));
                        }
                        else if (accumulation[ix, lasty] > mostAccumulation.Item1)
                        {
                            mostAccumulation = (accumulation[ix, lasty], (ix, lasty));
                        }
                    }
                    lastx = ix; lasty = iy;
                }
                int startAcc = mostAccumulation.Item1;
                int startX = mostAccumulation.Item2.Item1;
                int startY = mostAccumulation.Item2.Item2;
                int thisSubcat = subcatchments[mostAccumulation.Item2.Item1, mostAccumulation.Item2.Item2];
                float minElev = 99999;
                float accElev = 0;
                float accDistance = 0;
                bool keepIterating = true;
                bool firstIteration = true;
                while (keepIterating)
                {
                    keepIterating = false;
                    int x = mostAccumulation.Item2.Item1;
                    int y = mostAccumulation.Item2.Item2;


                    mostAccumulation = (-999, (0, 0)); // (Accumulation, (x,y))
                    for (int i = x - 1; i <= x + 1; i++)
                    {
                        if (i < 0 || i > numRows - 1) { continue; }
                        for (int j = y - 1; j <= y + 1; j++)
                        {
                            if (i == x && j == y) { continue; }
                            if (j < 0 || j > numCols - 1) { continue; }
                            if (elev[i, j] == noData_val) { continue; }
                            if (catchments[i, j] == default) { continue; }

                            //if (drainageMap[i, j] == dd[(i - x, j - y)])
                            if (drainageMap[i, j] == d8[i - x + 1, j - y + 1])
                            {
                                if (accumulation[i, j] > mostAccumulation.Item1 && accumulation[i, j] < accumulation[x, y])
                                {
                                    if (subcatchments[i, j] == subcatchments[x, y] && accumulation[i,j] > catchMaxAccs[subcatchments[x,y] - 1] * 0.125)
                                    {
                                        mostAccumulation = (accumulation[i, j], (i, j));
                                        keepIterating = true;
                                    }
                                    else
                                    {
                                        mostAccumulation = (accumulation[i, j], (i, j));
                                        keepIterating = false;
                                    }
                                }
                            }
                        }
                    }
                    if (firstIteration)
                    {
                        firstIteration = false;
                        continue;
                    }
                    if (keepIterating) 
                    { 
                        channel[x, y] = subcatchments[x, y];
                        float distance = (float)Math.Sqrt(Math.Pow((mostAccumulation.Item2.Item1 - x)*dx, 2) + Math.Pow((mostAccumulation.Item2.Item2 - y)*dy, 2));
                        float elevation = elev[mostAccumulation.Item2.Item1, mostAccumulation.Item2.Item2];
                        minElev = Math.Min(minElev, elevation);
                        accDistance += distance;
                        accElev += distance * elevation;
                    }

                }
                channelSlopes[thisSubcat - 1] = ((accElev / accDistance) - minElev) / (accDistance / 2);
                channelLengths[thisSubcat - 1] = accDistance;
                // Calculate catchment equal area slope
                minElev = 99999;
                accElev = 0;
                accDistance = 0;
                keepIterating = true;
                firstIteration = true;

                mostAccumulation = (startAcc, (startX, startY));
                // Calculate Subcatchment equal area slope
                while (keepIterating)
                {
                    keepIterating = false;
                    int x = mostAccumulation.Item2.Item1;
                    int y = mostAccumulation.Item2.Item2;


                    mostAccumulation = (-999, (0, 0)); // (Accumulation, (x,y))
                    for (int i = x - 1; i <= x + 1; i++)
                    {
                        if (i < 0 || i > numRows - 1) { continue; }
                        for (int j = y - 1; j <= y + 1; j++)
                        {
                            if (i == x && j == y) { continue; }
                            if (j < 0 || j > numCols - 1) { continue; }
                            if (elev[i, j] == noData_val) { continue; }
                            if (catchments[i, j] == default) { continue; }

                            //if (drainageMap[i, j] == dd[(i - x, j - y)])
                            if (drainageMap[i, j] == d8[i - x + 1, j - y + 1])
                            {
                                if (accumulation[i, j] > mostAccumulation.Item1 && accumulation[i, j] < accumulation[x, y])
                                {

                                    mostAccumulation = (accumulation[i, j], (i, j));
                                    keepIterating = true;

                                }
                            }
                        }
                    }
                    if (firstIteration)
                    {
                        firstIteration = false;
                        continue;
                    }
                    if (keepIterating)
                    {
                        float distance = (float)Math.Sqrt(Math.Pow((mostAccumulation.Item2.Item1 - x) * dx, 2) + Math.Pow((mostAccumulation.Item2.Item2 - y) * dy, 2));
                        float elevation = elev[mostAccumulation.Item2.Item1, mostAccumulation.Item2.Item2];
                        minElev = Math.Min(minElev, elevation);
                        accDistance += distance;
                        accElev += distance * elevation;
                    }

                }
                channelCatchSlopes[thisSubcat - 1] = ((accElev / accDistance) - minElev) / (accDistance / 2);
                channelCatchLengths[thisSubcat - 1] = accDistance;
            }
            Console.WriteLine("Stream Paths Written");
            List<float> timeOfConcentrations = new List<float>();
            List<float> upstreamAreas = new List<float>();
            for (int i = 0; i < channelLengths.Count; i++)
            {
                
                upstreamAreas.Add((float)Math.Round(catchMaxAccs[i] * dx * dy / 1000000f, 5));
                timeOfConcentrations.Add((float)Math.Round(58 * (channelCatchLengths[i] / 1000f) / ((float)Math.Pow(catchMaxAccs[i] * dx * dy / 1000000f, 0.1f) * (float)Math.Pow(channelCatchSlopes[i] * 1000f, 0.2f)), 5));
            }
            stopwatch.Stop();
            Console.WriteLine($"Time of concentration. Time: {stopwatch.ElapsedMilliseconds}");

            // VEC FILE WRITING
            stopwatch.Start();
            foreach (Subcatchment subcatchment in subcatchmentsInfo.Values)
            {
                subcatchment.numUS = subcatchment.upstreamCatchments.Count;
            }

            using (StreamWriter writer = new StreamWriter("TempVecFile.vec"))
            {
                writer.WriteLine("{MODEL NAME}");


                bool upstreamCatch = true;
                int outlets = 0;
                while (upstreamCatch)
                {
                    List<Subcatchment> queue = new List<Subcatchment>();
                    upstreamCatch = false;
                    foreach (Subcatchment subcatchment in subcatchmentsInfo.Values)
                    {
                        if (subcatchment.upstreamCatchments.Count == 0 && subcatchment.rooted == false)
                        {
                            queue.Add(subcatchment);
                            break;
                        }

                    }

                    while (queue.Count > 0)
                    {
                        // if no. upstream > 0, insert subcatchment at 0, and continue else ADD RAIN or RAIN, then ROUTE THRU deleting DS US catchment

                        if (queue[0].upstreamCatchments.Count > 0) { queue.Add(queue[0].upstreamCatchments[0]); queue.RemoveAt(0); continue; }
                        queue[0].rooted = true;
                        if (queue[0].numUS == 0) { writer.WriteLine($"RAIN #{queue[0].id}"); }
                        else { writer.WriteLine($"ADD RAIN #{queue[0].id}"); }

                        if (queue[0].dsCatchment != -1)
                        {

                            queue.Add(subcatchmentsInfo[queue[0].dsCatchment]);
                            int indexToRemove = 0;

                            foreach (Subcatchment subCat in subcatchmentsInfo[queue[0].dsCatchment].upstreamCatchments)
                            {
                                if (subCat.id == queue[0].id) { break; }
                                indexToRemove++;
                            }

                            subcatchmentsInfo[queue[0].dsCatchment].upstreamCatchments.RemoveAt(indexToRemove);

                        }

                        queue.RemoveAt(0);
                        if (queue.Count > 0)
                        {
                            if (queue[0].upstreamCatchments.Count > 0)
                            {
                                writer.WriteLine($"STORE.");
                            }
                            else
                            {
                                for (int usSubCats = 1; usSubCats < queue[0].numUS; usSubCats++)
                                {
                                    writer.WriteLine("GET.");
                                }
                                writer.WriteLine($"ROUTE THRU #{queue[0].id}");
                            }
                        }
                    }

                    foreach (Subcatchment subcatchment in subcatchmentsInfo.Values) // ADD DONE SUBCATCHMENTS
                    {
                        if (subcatchment.rooted == false)
                        {
                            writer.WriteLine("STORE.");
                            outlets += 1;
                            upstreamCatch = true;
                            break;
                        }
                    }
                }

                for (int i = 0; i < outlets; i++)
                {
                    writer.WriteLine("GET.");
                }

                writer.WriteLine("END OF CATCHMENT DATA.");
            }
            stopwatch.Stop();
            Console.WriteLine($"Vec File Written. Time: {stopwatch.ElapsedMilliseconds}");

            Dictionary<int, List<(float, float)>> catchmentSlopes = new Dictionary<int, List<(float, float)>>(); // Slope, Distance for distance weighted average
            for (int i = 1; i < catchmentID; i++)
            {
                catchmentSlopes.Add(i, new List<(float, float)>());
            }

            foreach ((int topX, int topY) in catchList)
            {

                int slopeCatch = subcatchments[topX, topY];
                if (subcatchments[topX, topY] == default)
                {
                    continue;
                }
                float distance = 0;
                float minElev = 99999;
                float sumElevDist = 0;
                if (catchmentSlopes[slopeCatch].Count >= 50)
                {
                    continue;
                }
                int currentCellX = topX;
                int currentCellY = topY;

                while (true)
                {
                    if (drainageMap[currentCellX, currentCellY] == default) { break; }
                    (int, int) cellDrainageDirection = reverseD8[drainageMap[currentCellX, currentCellY]];

                    int nextCellX = currentCellX - cellDrainageDirection.Item1;
                    int nextCellY = currentCellY - cellDrainageDirection.Item2;

                    if (channel[nextCellX, nextCellY] == default || accumulation[nextCellX, nextCellY] < catchMaxAccs[slopeCatch - 1] * 0.125)
                    {
                        float cellDistance = (float)Math.Sqrt(Math.Pow((currentCellX - nextCellX) * dx, 2) + Math.Pow((currentCellY - nextCellY) * dy, 2));
                        distance += cellDistance;
                        minElev = Math.Min(minElev, elev[nextCellX, nextCellY]);
                        sumElevDist += cellDistance * elev[nextCellX, nextCellY];
                        currentCellX = nextCellX;
                        currentCellY = nextCellY;
                    }
                    else
                    {
                        break;
                    }
                }
                if (distance > 0)
                {
                    catchmentSlopes[slopeCatch].Add((((sumElevDist / distance) - minElev) / (distance / 2), distance));
                }
            }
            
            using (StreamWriter writer = new StreamWriter("TempSubCats.csv"))
            {
                writer.WriteLine("Index,Area,L,Sc,N,HL,HS,k,d,I,UL,UM,UH,UF");
                for (int i = 1; i < catchmentID; i++)
                {
                    List<(float, float)> weightedSlopes = catchmentSlopes[i];
                    float weightedSum = weightedSlopes.Sum(item => item.Item1 * item.Item2);
                    float totalLength = weightedSlopes.Sum(item => item.Item2);
                    float hillLength = totalLength / weightedSlopes.Count;
                    float weightedAverage = weightedSum / totalLength;
                    writer.WriteLine($"{i},{Math.Round(catchAccs[i-1] * dx * dy / 1000000, 5)},{Math.Round(channelLengths[i - 1] / 1000, 5)}," +
                        $"{Math.Round(Math.Max(channelSlopes[i - 1], 0.0005), 5)},0.03,{Math.Round(hillLength/1000, 5)}," +
                        $"{Math.Round(weightedAverage, 5)},{Math.Round(catchHydraulicParameters[i - 1].Item1, 5)}," +
                        $"{Math.Round(catchHydraulicParameters[i - 1].Item2, 5)},0.0,0.0,0.0,0.0,0.0");
                }
                
            }

                Console.WriteLine($"Determined Catchment Slope");


            return (subcatchments, channel, accumulation, timeOfConcentrations, upstreamAreas);
        }
    }
}
