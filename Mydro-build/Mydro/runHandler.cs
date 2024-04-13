using Mydro;
using Microsoft.VisualBasic.FileIO;
using Mydro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Transput_Handler;
using System.Runtime.InteropServices;

namespace Run_Handler
{

    class runHandler
    {
        public static T DeepClone<T>(T source)
        {
            // Using JSON serialization for deep cloning
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(T));

            using (MemoryStream stream = new MemoryStream())
            {
                jsonSerializer.WriteObject(stream, source);
                stream.Position = 0;

                return (T)jsonSerializer.ReadObject(stream);
            }
        }

        public static Dictionary<string, List<double>> extractRecordedData(string recordedDataPath, List<double> timeVals)
        {
            // Recorded Data to be used for comparison of goodness of fit
            Dictionary<string, List<double>> extractedData = new Dictionary<string, List<double>>();
            Dictionary<string, string> indexOfCols = new Dictionary<string, string>();
            using (TextFieldParser parser = new TextFieldParser(recordedDataPath))
            {
                // Set the delimiter for the CSV file
                parser.Delimiters = new string[] { "," };

                // Read the fields while the end of the file is not reached
                while (!parser.EndOfData)
                {
                    List<string> fields = parser.ReadFields().ToList();
                    List<string> subset = fields.GetRange(1, fields.Count - 1);
                    if (timeVals.Count <= 0)
                    {
                        break;
                    }
                    // Indexing of columns to match subcatchment IDs 
                    if (fields[0].Length >= 4)
                    {
                        if (fields[0].Substring(0, 4).ToLower() == "time")
                        {
                            for (int i = 0; i < subset.Count; i++)
                            {
                                if (subset[i].Length > 0)
                                {
                                    extractedData.Add(subset[i], new List<double>() { 0 });
                                    indexOfCols.Add(i.ToString(), subset[i].ToString());
                                }
                            }
                            continue;
                        }
                    }

                    double timeVal = Convert.ToDouble(fields[0]) * 3600;
                    if (timeVals[0] < timeVal)
                    {
                        int iterationLimit = 1000000;
                        while (Math.Round(timeVals[0], 5) < Math.Round(timeVal, 5) && iterationLimit > 0)
                        {
                            for (int i = 0; i < subset.Count; i++)
                            {
                                // For the subcatchment ID add the target value for timestep
                                if (subset[i].Length > 0)
                                {
                                    string location = indexOfCols[i.ToString()];
                                    extractedData[location][extractedData[location].Count - 1] = (extractedData[location][extractedData[location].Count - 1] + Convert.ToDouble(subset[i])) / 2;
                                    extractedData[location].Add(Convert.ToDouble(subset[i]));
                                }
                            }
                            if (timeVals.Count > 1)
                            {
                                timeVals = timeVals.GetRange(1, timeVals.Count - 1);
                            }
                            else
                            {
                                timeVals.Clear();
                                break;
                            }
                            iterationLimit--;
                        }
                    }
                    else if (Math.Round(timeVals[0], 5) == Math.Round(timeVal, 5))
                    {
                        for (int i = 0; i < subset.Count; i++)
                        {
                            if (subset[i].Length > 0)
                            {
                                // For the subcatchment ID add the target value for timestep
                                string location = indexOfCols[i.ToString()];
                                extractedData[location][extractedData[location].Count - 1] = Convert.ToDouble(subset[i]);
                                extractedData[location].Add(Convert.ToDouble(subset[i]));
                            }
                        }
                        if (timeVals.Count > 0)
                        {
                            timeVals = timeVals.GetRange(1, timeVals.Count - 1);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < subset.Count; i++)
                        {
                            if (subset[i].Length > 0)
                            {
                                // For the subcatchment ID add the target value for timestep
                                string location = indexOfCols[i.ToString()];
                                extractedData[location][extractedData[location].Count - 1] = Convert.ToDouble(subset[i]);
                            }
                        }
                    }

                }
            }
            return extractedData;
        }
        static void Main(string[] args)
        { 
            fileLocations FileLocations = new fileLocations(args[0]);
            GlobalVariables.CL = FileLocations.CL;
            GlobalVariables.X = FileLocations.X;
            GlobalVariables.NonLin = FileLocations.N;

            // id, area, reachL, reachF, channelSlope, catchslope, fracforested, fracUrban, mannings 

            List<Subcatchment> listOfSubbys = new List<Subcatchment>();
            if (FileLocations.run == "cal") // Calibration Run
            {
                Console.WriteLine("CALIBRATION RUN");
                GlobalVariables.IL = double.Parse(FileLocations.IL);
                List<string> orderOfProcessing;
                getCatch_var inputData = new getCatch_var();
                GlobalVariables.IntermediateSteps = 1;

                orderOfProcessing = inputData.ProcessVecFile(FileLocations.vectorFile, GlobalVariables.Dt, FileLocations.dbaseFile, "", "", "");

                Dictionary<string, List<double>> groupedRainfall = inputData.groupedAreas;
                inputData.ProcessCatFile(FileLocations.catchmentFile);

                foreach (string id in orderOfProcessing)
                {
                    Subcatchment subcatchment = new Subcatchment(id, new double[] {inputData.catInfo[id].Area, inputData.catInfo[id].L, inputData.catInfo[id].Sc,
                        inputData.catInfo[id].HS, inputData.catInfo[id].F, inputData.catInfo[id].U, inputData.catInfo[id].N, inputData.catInfo[id].I,
                        inputData.catInfo[id].kappa, inputData.catInfo[id].delta, inputData.catInfo[id].HL });

                    if (inputData.vecInfo[id].sq.Count > 0)
                    {
                        subcatchment.storages = inputData.vecInfo[id].sq;
                    }

                    foreach (string upstreamId in inputData.vecInfo[id].UpstreamSubcatchments)
                    {
                        int upstreamIdInt = Convert.ToInt32(upstreamId);
                        foreach (Subcatchment possibleCats in listOfSubbys)
                        {
                            if (upstreamIdInt == possibleCats.id)
                            {
                                subcatchment.upstreamSubby.Add(possibleCats);
                            }
                        }
                    }
                    listOfSubbys.Add(subcatchment);
                }

                List<double> timeseries = new List<double>();
                for (int i = 0; i < groupedRainfall["1"].Count; i++)
                {
                    timeseries.Add(i * GlobalVariables.Dt);
                }
                Dictionary<string, List<double>> result = extractRecordedData(FileLocations.recordedFlows, timeseries);
                GlobalVariables.recordedFlows = result;
                GlobalVariables.Dt = GlobalVariables.Dt / GlobalVariables.IntermediateSteps;

                double alphaDensity = 45, minAlpha = 0.05, maxAlpha = 0.5, betaDensity = 40, minBeta = 1, maxBeta = 9.0, mDensity = 10, minM = 0.6, maxM = 0.8;
                double alphaConvergeMax = maxAlpha + 1, alphaConvergeMin = minAlpha - 1, betaConvergeMax = maxBeta + 1, betaConvergeMin = minAlpha - 1, mConvergeMax = maxM + 1, mConvergeMin = minM - 1;
                int alphaRounding = 2;

                if (inputData.lowerAlpha)
                {
                    minAlpha = 0.0005;
                    maxAlpha = 0.01;
                    alphaRounding = 4;
                }

                double[] alphaRange = Enumerable.Range(0, (int)((maxAlpha - minAlpha) / ((maxAlpha - minAlpha) / alphaDensity)) + 1).Select(i => Math.Round(minAlpha + i * ((maxAlpha - minAlpha) / alphaDensity), alphaRounding)).ToArray();
                double[] betaRange = Enumerable.Range(0, (int)((maxBeta - minBeta) / ((maxBeta - minBeta) / betaDensity)) + 1).Select(i => Math.Round(minBeta + i * ((maxBeta - minBeta) / betaDensity), 2)).ToArray();
                double[] mRange = Enumerable.Range(0, (int)((maxM - minM) / ((maxM - minM) / mDensity)) + 1).Select(i => Math.Round(minM + i * ((maxM - minM) / mDensity), 2)).ToArray();

                List<List<double>> combinations = new List<List<double>>();

                foreach (double alpha in alphaRange)
                {
                    foreach (double beta in betaRange)
                    {
                        // combinations.Add(new List<double> { alpha, beta, 0.62 });
                        foreach (double m in mRange)
                        {
                            combinations.Add(new List<double> { alpha, beta, m });
                        }
                    }
                }
                Random random = new Random();
                combinations = combinations.OrderBy(arr => random.Next()).ToList();
                PriorityQueue<List<double>, double> bestFits = new PriorityQueue<List<double>, double>();
                List<double> currentBestFit = new List<double> { 0, 0, 0 };
                int count = 0;
                List<List<float>> exclusionList = new List<List<float>>();
                foreach (List<double> combination in combinations)
                {
                    if (count % 250 == 0 && count > 0)
                    {
                        List<double> alphas = new List<double>();
                        List<double> betas = new List<double>();
                        List<double> ms = new List<double>();

                        List<double> priorities = new List<double>();

                        for (int i = 0; i < 20; i++)
                        {
                            bestFits.TryDequeue(out List<double> combo, out double priority);
                            alphas.Add(combo[0]);
                            betas.Add(combo[1]);
                            ms.Add(combo[2]);
                            priorities.Add(priority);
                        }

                        alphaConvergeMax = alphas.Max();
                        alphaConvergeMin = alphas.Min();
                        betaConvergeMax = betas.Max();
                        betaConvergeMin = betas.Min();
                        mConvergeMax = ms.Max();
                        mConvergeMin = ms.Min();

                        for (int i = 0; i < priorities.Count; i++)
                        {
                            bestFits.Enqueue(new List<double>() { alphas[i], betas[i], ms[i] }, priorities[i]);
                        }

                    }

                    if (combination[0] > alphaConvergeMax || combination[0] < alphaConvergeMin || combination[1] > betaConvergeMax || combination[1] < betaConvergeMin || combination[2] > mConvergeMax || combination[2] < mConvergeMin)
                    {
                        count++;
                        continue;
                    }

                    List<Subcatchment> currentSubbys = DeepClone(listOfSubbys);

                    double qualityFit = Program.runoffRouting(groupedRainfall, combination, currentSubbys, null);
                    if (double.IsNaN(qualityFit))
                    {
                        qualityFit = 9999999;
                    }
                    bestFits.Enqueue(combination, qualityFit);
                    if (bestFits.Peek() != currentBestFit)
                    {
                        currentBestFit = bestFits.Peek();
                        Console.WriteLine($"({count.ToString("00000")}/{combinations.Count}) Best Combination- Alpha:{Math.Round(bestFits.Peek()[0], alphaRounding)} Beta:{bestFits.Peek()[1].ToString("0.0")} m:{bestFits.Peek()[2].ToString("0.00")}");
                    }
                    count++;
                }

                Program.runoffRouting(groupedRainfall, bestFits.Peek(), DeepClone(listOfSubbys), FileLocations.outputFile);
                if (File.Exists(FileLocations.fitsFileName))
                {
                    while (Program.IsFileLocked(FileLocations.fitsFileName))
                    {
                        Console.WriteLine($"{FileLocations.fitsFileName} file is locked. Please Close File and Press Enter to Retry.");
                        Console.ReadLine(); // This line will wait for the user to press Enter
                    }
                }
                using (StreamWriter writer = new StreamWriter(FileLocations.fitsFileName))
                {
                    writer.WriteLine($"Rank,Error,Alpha,Beta,m");
                    int lengthOfQueue = bestFits.Count;
                    for (int i = 0; i < lengthOfQueue; i++)
                    {
                        bestFits.TryDequeue(out List<double> combo, out double priority);
                        writer.WriteLine($"{i + 1},{Math.Round(priority, 3)},{combo[0]},{combo[1]},{combo[2]}");
                    }
                }
            }
            else if (FileLocations.run == "batch") // DESIGN RUNS
            {
                Console.WriteLine("BATCH RUN MODE");
                List<double> parameters = new List<double>() { FileLocations.alpha, FileLocations.beta, FileLocations.m };
                if (!Directory.Exists(FileLocations.outputDir))
                {
                    Directory.CreateDirectory(FileLocations.outputDir);
                }

                foreach (string aep in FileLocations.aeps)
                {
                    foreach (string dur in FileLocations.durations)
                    {
                        foreach (string ens in FileLocations.ensembles)
                        {

                            if (double.TryParse(FileLocations.IL, out _))
                            {
                                GlobalVariables.IL = double.Parse(FileLocations.IL);
                            }
                            else
                            {
                                using (TextFieldParser parser = new TextFieldParser(FileLocations.IL.Replace("\\", "\\\\")))
                                {
                                    // Set the delimiter for the CSV file
                                    parser.Delimiters = new string[] { "," };
                                    int column = 0;
                                    List<string> events = new List<string>() { aep, dur, ens };
                                    // Read the fields while the end of the file is not reached
                                    while (!parser.EndOfData)
                                    {
                                        List<string> fields = parser.ReadFields().ToList();
                                        
                                        if (fields.Count > 0)
                                        {
                                            if (fields[0].Length > 0) { if (fields[0][0] == '!') { continue; } }
                                            if (column == 0)
                                            {
                                                foreach (string eve in events)
                                                {
                                                    int index = fields.IndexOf(eve);
                                                    if (index != -1)
                                                    {
                                                        column = index;
                                                    }
                                                }
                                                if (column == 0)
                                                {
                                                    Console.WriteLine($"ERROR: Column not found for either {aep}, {dur}, or {ens} in {FileLocations.IL}");
                                                    Environment.Exit(0);
                                                }
                                            }
                                            else
                                            {
                                                foreach (string eve in events)
                                                {
                                                    int index = fields.IndexOf(eve);
                                                    if (index != 0 && index != -1)
                                                    {
                                                        GlobalVariables.IL = double.Parse(fields[column]);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            foreach (KeyValuePair<List<string>, double> preburst in FileLocations.prebursts)
                            {
                                List<string> events = new List<string> { aep, dur, ens };
                                bool allInReferenceList = preburst.Key.All(item => events.Contains(item));
                                if (allInReferenceList)
                                {
                                    GlobalVariables.IL = preburst.Value;
                                }
                            }

                            List<Subcatchment> listOfDesSubbys = new List<Subcatchment>();
                            getCatch_var inputData = new getCatch_var();
                            string outputPath = Path.Combine(FileLocations.outputDir, $"{aep}_{dur}_{ens}.csv");
                            List<string> orderOfProcessing = inputData.ProcessVecFile(FileLocations.vectorFile, GlobalVariables.Dt, FileLocations.dbaseFile, aep, dur, ens); // Change For Design Rainfall
                            Dictionary<string, List<double>> groupedRainfall = inputData.groupedAreas;
                            inputData.ProcessCatFile(FileLocations.catchmentFile);

                            foreach (string id in orderOfProcessing)
                            {
                                Subcatchment subcatchment = new Subcatchment(id, new double[] { inputData.catInfo[id].Area, inputData.catInfo[id].L, inputData.catInfo[id].Sc,
                                    inputData.catInfo[id].HS, inputData.catInfo[id].F, inputData.catInfo[id].U, inputData.catInfo[id].N, inputData.catInfo[id].I,
                                    inputData.catInfo[id].kappa, inputData.catInfo[id].delta, inputData.catInfo[id].HL });

                                if (inputData.vecInfo[id].sq.Count > 0)
                                {
                                    subcatchment.storages = inputData.vecInfo[id].sq;
                                }

                                foreach (string upstreamId in inputData.vecInfo[id].UpstreamSubcatchments)
                                {
                                    int upstreamIdInt = Convert.ToInt32(upstreamId);
                                    foreach (Subcatchment possibleCats in listOfDesSubbys)
                                    {
                                        if (upstreamIdInt == possibleCats.id)
                                        {
                                            subcatchment.upstreamSubby.Add(possibleCats);
                                        }
                                    }
                                }
                                listOfDesSubbys.Add(subcatchment);
                            }

                            GlobalVariables.Dt = GlobalVariables.Dt / GlobalVariables.IntermediateSteps;
                            Program.runoffRouting(groupedRainfall, parameters, listOfDesSubbys, outputPath);
                            GlobalVariables.Dt = GlobalVariables.Dt * GlobalVariables.IntermediateSteps;
                        }
                    }
                }
            }
            else if (FileLocations.run == "single")
            {
                Console.WriteLine("SINGLE RUN");
                List<double> parameters = new List<double>() { FileLocations.alpha, FileLocations.beta, FileLocations.m };

                GlobalVariables.IL = double.Parse(FileLocations.IL);

                List<Subcatchment> listOfDesSubbys = new List<Subcatchment>();
                getCatch_var inputData = new getCatch_var();
                string outputPath = FileLocations.outputFile;
                List<string> orderOfProcessing = inputData.ProcessVecFile(FileLocations.vectorFile, GlobalVariables.Dt, FileLocations.dbaseFile, "", "", ""); // Change For Design Rainfall
                Dictionary<string, List<double>> groupedRainfall = inputData.groupedAreas;
                inputData.ProcessCatFile(FileLocations.catchmentFile);

                foreach (string id in orderOfProcessing)
                {
                    Subcatchment subcatchment = new Subcatchment(id, new double[] { inputData.catInfo[id].Area, inputData.catInfo[id].L, inputData.catInfo[id].Sc,
                                    inputData.catInfo[id].HS, inputData.catInfo[id].F, inputData.catInfo[id].U, inputData.catInfo[id].N, inputData.catInfo[id].I,
                                    inputData.catInfo[id].kappa, inputData.catInfo[id].delta, inputData.catInfo[id].HL });

                    if (inputData.vecInfo[id].sq.Count > 0)
                    {
                        subcatchment.storages = inputData.vecInfo[id].sq;
                    }

                    foreach (string upstreamId in inputData.vecInfo[id].UpstreamSubcatchments)
                    {
                        int upstreamIdInt = Convert.ToInt32(upstreamId);
                        foreach (Subcatchment possibleCats in listOfDesSubbys)
                        {
                            if (upstreamIdInt == possibleCats.id)
                            {
                                subcatchment.upstreamSubby.Add(possibleCats);
                            }
                        }
                    }
                    listOfDesSubbys.Add(subcatchment);
                }

                GlobalVariables.Dt = GlobalVariables.Dt / GlobalVariables.IntermediateSteps;

                Program.runoffRouting(groupedRainfall, parameters, listOfDesSubbys, outputPath);
                GlobalVariables.Dt = GlobalVariables.Dt * GlobalVariables.IntermediateSteps;

            }
        }
    }
}
