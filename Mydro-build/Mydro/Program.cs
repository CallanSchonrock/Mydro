using Transput_Handler;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using System.Runtime.Serialization.Formatters.Binary;
using System.CodeDom;
using System.Runtime.Serialization.Json;

namespace Mydro
{
    // Global variables be careful with Dt, it changes to Dt / Intermediate Steps within the script
    public static class GlobalVariables
    {
        public static double Alpha = 1.0, Beta = 4.0, m = 0.8, NonLin = 1, X = 0.1, Dt = (double) (1d / 12d) * 3600d, IL = 0, CL = 0, IntermediateSteps = 4, ilRecoveryRate = 0;
        public static Dictionary<string, List<double>> recordedFlows = new Dictionary<string, List<double>>();
    }

    // Rainfall Excess class used for Time-Area Diagram
    [Serializable]
    class rainfallExcess
    {
        public Double TimePassed = 0, TimeArea = 0, RainfallDepth;
    }

    // Main Subcatchment class instances represent individual subcatchments
    [Serializable]
    class Subcatchment
    {
        public double inflow = 0, outflows = 0, channelStorage = 0, catchStorage = 0, catchDischarge = 0;
        public List<Subcatchment> upstreamSubby = new List<Subcatchment>();
        public int id;
        public double catchIL; // Different to other subcatchments with different rainfall time series
        public double area, reachL, reachF, channelSlope, catchVelocity, fracForested, fracUrban, mannings, fracImpervious, kappa, delta, hillLength, linHydraulicArea, linHydraulicSlope;
        public List<(List<(double, double)>, double)> storages = new List<(List<(double, double)>, double)>();
        public List<(List<(double, double)>, double)> storageAreas = new List<(List<(double, double)>, double)>();
        public List<rainfallExcess> subcatchmentExcessRainfall = new List<rainfallExcess>(); // List of rainfall on subcatchment within the Time-Area Diagram

        public Subcatchment(string inputID, double[] args)
        {
            id = Convert.ToInt32(inputID);
            area = args[0];
            reachL = args[1];
            channelSlope = args[2];
            if (channelSlope == 0.1) { channelSlope = 1; }
            double catchSlope = args[3];

            fracForested = args[4];
            fracUrban = args[5];
            mannings = args[6];
            fracImpervious = args[7];
            kappa = args[8];
            delta = args[9];
            linHydraulicArea = -1; linHydraulicSlope = 0;
            if (delta < 0)
            {
                linHydraulicArea = Math.Pow(Math.E, (-delta) / kappa) * 2;
                linHydraulicSlope = (kappa * Math.Log(linHydraulicArea) + delta) / linHydraulicArea;
            }

            hillLength = args[10];
            catchIL = GlobalVariables.IL;
            // Slope velocity from ARR87
            if (catchSlope <= 0.015) { catchVelocity = 1.1 / 3.6; }
            else if (catchSlope <= 0.04) { catchVelocity = 2.5 / 3.6; }
            else if (catchSlope <= 0.08) { catchVelocity = 3.2 / 3.6; }
            else if (catchSlope <= 0.15) { catchVelocity = 5.2 / 3.6; }
            else { catchVelocity = 10.8 / 3.6; }
        }
        public void printCatchmentCharacteristics()
        {
            Console.WriteLine($"ID: {id}");
            Console.WriteLine($"Area: {area}");
            Console.WriteLine($"Reach Length: {reachL}");
            Console.WriteLine($"Channel Slope: {channelSlope}");
            Console.WriteLine($"Catch Velocity: {catchVelocity}");
            Console.WriteLine($"Frac Forested: {fracForested}");
            Console.WriteLine($"Frac Urban: {fracUrban}");
            Console.WriteLine($"Frac Mannings: {mannings}");
            Console.WriteLine($"Frac Impervious: {fracImpervious}");
            Console.WriteLine($"Remaining Catchment IL: {catchIL}");
            Console.WriteLine($"Storages: {storages.Count}");
            Console.WriteLine($"Upper Catchment Rainfall: {subcatchmentExcessRainfall.Count}");
            Console.WriteLine($"Immediate Upstream Subbies: {upstreamSubby.Count}");
            Console.WriteLine($"Inflow: {inflow}");
            Console.WriteLine($"Outflows: {outflows}");
            Console.WriteLine($"Channel Storage: {channelStorage}");
            Console.WriteLine($"Catch Storage: {catchStorage}");
            Console.WriteLine("\n");
            Thread.Sleep(1000);
        }
        public void updateCatchment(double totalRainfall)
        {
            // printCatchmentCharacteristics();
            // Put rainfall into time area diagram
            Rain_on_catchment(totalRainfall);
            // Reduce catchment specific IL
            if (totalRainfall == 0)
            {
                catchIL = Math.Max(catchIL + GlobalVariables.ilRecoveryRate * GlobalVariables.Dt, GlobalVariables.IL);
            }
            catchIL = Math.Max(0, catchIL - totalRainfall);
            // Route catchment excess water through storage after time area diagram into channel storage
            CatchDischarge();
            // Get upstream channel inflows
            UpstreamInflow();
            // Route Channel Storage into outflow
            /*
            if (id == 1)
            {
                Console.WriteLine(totalRainfall);
                Console.WriteLine("Before Muskingum");
                printCatchmentCharacteristics();
            }
            */
            Muskingum();
            /*
            if (id == 1)
            {
                Console.WriteLine("After Muskingum");
                printCatchmentCharacteristics();
            }
            */
        }

        private void Rain_on_catchment(double total_rainfall)
        {
            // Excess Rainfall = Pervious Rainfall - Storm Losses + Impervious Rainfall 
            double excess_rainfall = (total_rainfall * (1 - fracImpervious)) - catchIL;
            excess_rainfall = Math.Max(excess_rainfall - GlobalVariables.CL * (GlobalVariables.Dt / 3600), 0);
            excess_rainfall += total_rainfall * fracImpervious;
            excess_rainfall = excess_rainfall / 1000; // Conversion to rainfall depth in m

            if (excess_rainfall > 0)
            {
                // Add to Time Area Diagram
                rainfallExcess newRainfall = new rainfallExcess();
                newRainfall.RainfallDepth = excess_rainfall;
                subcatchmentExcessRainfall.Add(newRainfall);
            }
            foreach (rainfallExcess rain in subcatchmentExcessRainfall) { rain.TimePassed += GlobalVariables.Dt; } // Increment Time-Area Diagram

            List<rainfallExcess> itemsToRemove = new List<rainfallExcess>(); // To Avoid indexing issues garbage collection at the end
            foreach (rainfallExcess i in subcatchmentExcessRainfall)
            {
                if ((catchVelocity * i.TimePassed / hillLength) >= 1.0)
                {
                    catchStorage += i.RainfallDepth * (area - i.TimeArea);
                    i.TimeArea = area;
                }
                else
                {
                    catchStorage += i.RainfallDepth * (catchVelocity * i.TimePassed / hillLength * area - i.TimeArea);
                    i.TimeArea = ((catchVelocity * i.TimePassed) / hillLength) * area;
                }
                if (i.TimeArea >= area) { itemsToRemove.Add(i); }
            }
            foreach (rainfallExcess itemToRemove in itemsToRemove) { subcatchmentExcessRainfall.Remove(itemToRemove); }

        }



        private void CatchDischarge()
        {
            // Full Flow is discharge on timestep emptied flow is discharge at end of timestep, these estimations are averaged
            double fullFlow = Math.Pow(catchStorage * Math.Pow(1 + fracUrban, 2) / (GlobalVariables.Beta * hillLength * Math.Pow(1 + fracForested, 2)), 1 / GlobalVariables.m);
            double emptiedFlow = Math.Pow((catchStorage - fullFlow * GlobalVariables.Dt) * Math.Pow(1 + fracUrban, 2) / (GlobalVariables.Beta * hillLength * Math.Pow(1 + fracForested, 2)), 1 / GlobalVariables.m);
            
            catchDischarge = (fullFlow + emptiedFlow) / 2;
            double totalDischarge = catchDischarge * GlobalVariables.Dt;
            channelStorage += totalDischarge;
            catchStorage -= totalDischarge;
            /*
            if (id == 1)
            {
                Console.WriteLine(catchStorage);
                Console.WriteLine(totalDischarge);
            }
            */
        }
        private void UpstreamInflow()
        {
            inflow = 0;
            foreach (Subcatchment sub in upstreamSubby)
            {
                channelStorage += sub.outflows * GlobalVariables.Dt;
                inflow += sub.outflows;
            }
        }

        private double getHydraulicC()
        {
            double conveyanceArea = channelStorage / reachL;
            double hydraulicCoefficient;
            if (conveyanceArea < linHydraulicArea)
            {
                hydraulicCoefficient = Math.Max(0.1,linHydraulicSlope * conveyanceArea);
            }
            else if (kappa == 0 && delta == 0)
            {
                hydraulicCoefficient = (double)Math.Max(0.1, 0.3 * Math.Log(conveyanceArea) + -0.3);
            }
            else
            {
                hydraulicCoefficient = Math.Max(0.1, kappa * Math.Log(conveyanceArea) + delta);
            }
            
            return hydraulicCoefficient;
        }

        private void Muskingum()
        {
            double fullFlow;
            double emptiedFlow;
            // URBS fullFlow = Math.Max((Math.Pow(channelStorage * Math.Sqrt(channelSlope) / (GlobalVariables.Alpha * reachF * reachL * mannings), 1 / GlobalVariables.NonLin) -GlobalVariables.X * inflow) / (1 - GlobalVariables.X), 0);
            if (kappa == 0 && delta == 0)
            {
                double conveyanceArea = channelStorage / reachL;
                double hydraulicCoefficient = getHydraulicC();
                fullFlow = Math.Max((Math.Pow(conveyanceArea * Math.Sqrt(channelSlope) * hydraulicCoefficient / (mannings * GlobalVariables.Alpha),
                    1 / GlobalVariables.NonLin) - GlobalVariables.X * inflow) / (1 - GlobalVariables.X), 0);
                conveyanceArea = (channelStorage - fullFlow * GlobalVariables.Dt) / reachL;
                hydraulicCoefficient = getHydraulicC();
                emptiedFlow = Math.Max((Math.Pow(conveyanceArea * Math.Sqrt(channelSlope) * hydraulicCoefficient / (mannings * GlobalVariables.Alpha),
                    1 / GlobalVariables.NonLin) - GlobalVariables.X * inflow) / (1 - GlobalVariables.X), 0);
            }
            else
            {
                double conveyanceArea = channelStorage / reachL;
                double hydraulicCoefficient = getHydraulicC();
                fullFlow = Math.Max((Math.Pow(conveyanceArea * Math.Sqrt(channelSlope) * hydraulicCoefficient / (mannings * GlobalVariables.Alpha),
                    1 / GlobalVariables.NonLin) - GlobalVariables.X * inflow) / (1 - GlobalVariables.X), 0);
                conveyanceArea = (channelStorage - fullFlow * GlobalVariables.Dt) / reachL;
                hydraulicCoefficient = getHydraulicC();
                emptiedFlow = Math.Max((Math.Pow(conveyanceArea * Math.Sqrt(channelSlope) * hydraulicCoefficient / (mannings * GlobalVariables.Alpha),
                    1 / GlobalVariables.NonLin) - GlobalVariables.X * inflow) / (1 - GlobalVariables.X), 0);
            }

            // Full Flow is discharge on timestep emptied flow is discharge at end of timestep, these estimations are averaged
            outflows = (fullFlow + emptiedFlow) / 2;
            double discharge = outflows * GlobalVariables.Dt;
            channelStorage -= discharge;
            for (int i = 0; i < storageAreas.Count; i++) // Number of storages in subcatchment
            {
                if (storageAreas[i].Item1.Count == 0)
                {
                    storages[i] = (storages[i].Item1, Math.Max(storages[i].Item2 - (storageAreas[i].Item2 / 86400) * GlobalVariables.Dt, 0));
                    continue;
                }
                double area = 0;
                for (int j = 0; j < storageAreas[i].Item1.Count; j++) // Loop through storage Discharge Pairs
                {
                    if (storageAreas[i].Item1[j].Item1 <= storages[i].Item2)  // If Storage (ML) in Storage Discharge Pair <= Current Storage
                    {
                        if (storageAreas[i].Item1.Count - 1 < j + 1) // If Current Storage exceeds last storage discharge Pair
                        {
                            area = storageAreas[i].Item1[j].Item2;
                        }
                        else
                        {
                            double gradient = (storageAreas[i].Item1[j + 1].Item2 - storageAreas[i].Item1[j].Item2) / (storageAreas[i].Item1[j + 1].Item1 - storageAreas[i].Item1[j].Item1);
                            area = storageAreas[i].Item1[j].Item2 + gradient * (storages[i].Item2 - storageAreas[i].Item1[j].Item1);
                        }
                        storages[i] = (storages[i].Item1, Math.Max(storages[i].Item2 - area * 1000000 * storageAreas[i].Item2 / 1000 / 86400 * GlobalVariables.Dt,0));
                    }
                }
            }

            for (int i = 0; i < storages.Count; i++) // Number of storages in subcatchment
            {
                storages[i] = (storages[i].Item1, storages[i].Item2 + discharge);
                for (int j = 0; j < storages[i].Item1.Count; j++) // Loop through storage Discharge Pairs
                {
                    if (storages[i].Item1[j].Item1 <= storages[i].Item2)  // If Storage (ML) in Storage Discharge Pair <= Current Storage
                    {
                        if (storages[i].Item1.Count - 1 < j + 1) // If Current Storage exceeds last storage discharge Pair
                        {
                            outflows = storages[i].Item1[j].Item2;
                        }
                        else
                        {
                            double gradient = (storages[i].Item1[j + 1].Item2 - storages[i].Item1[j].Item2) / (storages[i].Item1[j + 1].Item1 - storages[i].Item1[j].Item1);
                            outflows = storages[i].Item1[j].Item2 + gradient * (storages[i].Item2 - storages[i].Item1[j].Item1);
                        }
                    }
                }
                discharge = outflows * GlobalVariables.Dt;
                storages[i] = (storages[i].Item1, Math.Max(storages[i].Item2 - discharge,0));
            }


        }
    }

    class Program
    {
        public static bool IsFileLocked(string filePath)
        {
            try
            {
                using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return false; // File is not locked; it can be opened for reading.
                }
            }
            catch (IOException)
            {
                return true; // File is locked and cannot be opened for reading.
            }
        }


        public static double runoffRouting(Dictionary<string, List<double>> groupedRainfall, List<double> parameters, List<Subcatchment> currentSubbys, string? fileName)
        {
            GlobalVariables.Alpha = parameters[0];
            GlobalVariables.Beta = parameters[1];
            GlobalVariables.m = parameters[2];


            List<double> compart_error = new List<double>();
            double cum_error = 0;
            foreach (Subcatchment sub in currentSubbys)
            {
                if (GlobalVariables.recordedFlows.ContainsKey(sub.id.ToString()))
                {
                    compart_error.Add(0);
                }
            }

            if (File.Exists(fileName) && fileName != null)
            {

                while (IsFileLocked(fileName))
                {
                    Console.WriteLine($"{fileName} file is locked. Please Close File and Press Enter to Retry.");
                    Console.ReadLine(); // This line will wait for the user to press Enter
                }
            }
            StringBuilder content = new StringBuilder();
            StringBuilder localContent = new StringBuilder();
            if (fileName != null)
            {
                Console.WriteLine(fileName);
                /*
                content.AppendLine($"Alpha:,{parameters[0]},Beta:,{parameters[1]},m:,{parameters[2]}");
                localContent.AppendLine($"Alpha:,{parameters[0]},Beta:,{parameters[1]},m:,{parameters[2]}");

                content.Append($"SubCat");
                localContent.Append($"SubCat");
                foreach (Subcatchment sub in currentSubbys)
                {
                    content.Append($",{sub.id}");
                    localContent.Append($",{sub.id}");
                }
                content.AppendLine();
                localContent.AppendLine();
                content.Append($"Frac Urbanized");
                localContent.Append($"Frac Urbanized");
                foreach (Subcatchment sub in currentSubbys)
                {
                    content.Append($",{Math.Round(sub.fracUrban * 100, 2)}");
                    localContent.Append($",{Math.Round(sub.fracUrban * 100, 2)}");
                }
                content.AppendLine();
                localContent.AppendLine();
                content.Append($"Area");
                localContent.Append($"Area");
                foreach (Subcatchment sub in currentSubbys)
                {
                    content.Append($",{Math.Round(sub.area / 1000000, 3)}");
                    localContent.Append($",{Math.Round(sub.area / 1000000, 3)}");
                }
                content.AppendLine();
                localContent.AppendLine();
                */
                content.Append($"Time (hours)");
                localContent.Append($"Time (hours)");
                foreach (Subcatchment sub in currentSubbys)
                {
                    content.Append($",Q_{sub.id}");
                    localContent.Append($",Q_{sub.id}");
                }
                content.AppendLine();
                localContent.AppendLine();
            }

            for (int i = 0; i < groupedRainfall[groupedRainfall.Keys.ElementAt(0)].Count; i++)
            {
                if (fileName != null) { 
                    content.Append($"{Math.Round(i * GlobalVariables.Dt / 3600 * GlobalVariables.IntermediateSteps, 2)}");
                    localContent.Append($"{Math.Round(i * GlobalVariables.Dt / 3600 * GlobalVariables.IntermediateSteps, 2)}");
                }
                for (int j = 0; j < GlobalVariables.IntermediateSteps; j++)
                {
                    int indexCompError = 0;
                    foreach (Subcatchment sub in currentSubbys)
                    {
                        sub.updateCatchment(groupedRainfall[sub.id.ToString()][i] / GlobalVariables.IntermediateSteps);
                        if (j == 0)
                        {
                            if (fileName != null) { content.Append($",{Math.Round(sub.outflows, 4)}"); localContent.Append($",{Math.Round(sub.catchDischarge, 4)}"); }
                            if (GlobalVariables.recordedFlows != null && GlobalVariables.recordedFlows.ContainsKey(sub.id.ToString()))
                            {
                                if (GlobalVariables.recordedFlows[sub.id.ToString()].Count > i)
                                {
                                    double recordedFlow = GlobalVariables.recordedFlows[sub.id.ToString()][i];
                                    double maxFlow = GlobalVariables.recordedFlows[sub.id.ToString()].Max();
                                    if (recordedFlow > 0.05 * maxFlow)
                                    {
                                        double error_val = Math.Abs(recordedFlow - sub.outflows) / recordedFlow;
                                        if (Math.Max(recordedFlow, sub.outflows) > maxFlow / 3)
                                        {
                                            error_val = error_val * 2;
                                        }
                                        compart_error[indexCompError] += error_val * (Math.Pow(maxFlow, 1 / 5)) / (groupedRainfall["1"].Count / 10);
                                    }
                                }
                                indexCompError += 1;
                            }
                        }
                    }
                }
                if (fileName != null)
                { 
                    content.AppendLine();
                    localContent.AppendLine();
                }
            }
            if (fileName != null)
            {
                using (StreamWriter writer = new StreamWriter(fileName))
                {
                    writer.Write(content.ToString());
                }
                string localFileName = fileName.Insert(fileName.LastIndexOf('.'), "_local");
                using (StreamWriter writer = new StreamWriter(localFileName))
                {
                    writer.Write(localContent.ToString());
                }
            }

            foreach (double error in compart_error)
            {
                if (compart_error.Count > 3)
                {
                    if (compart_error.Max() != error)
                    {
                        cum_error += error;
                    }
                    else
                    {
                        cum_error += error * 2;
                    }
                }
                else
                {
                    cum_error += error;
                }
            }
            return cum_error;

        }
    }
}