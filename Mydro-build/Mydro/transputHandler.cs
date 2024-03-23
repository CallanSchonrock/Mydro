using System;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;

namespace Transput_Handler
{
    public class VecSubby
    {
        public string Id;
        public List<string> UpstreamSubcatchments = new List<string>();
        public List<(List<(double, double)>, double)> sq = new List<(List<(double, double)>, double)>();
        public double? VBF;
        public double? EVAP;
    }

    public class CatSubby
    {
        /* 
         * ID Unique ID
         * Area km^2
         * CS Catchment Slope
         * I Impervious Fraction
         * U Fraction Urban
         * F Fraction Forested
        */
        public string Id;
        public double L, Area, Sc, N, HL, HS, I, U, F, kappa = 0, delta = 0;
    }

    class getCatch_var
    {
        public Dictionary<string, VecSubby> vecInfo = new Dictionary<string, VecSubby>();
        public Dictionary<string, CatSubby> catInfo = new Dictionary<string, CatSubby>();
        public Dictionary<string, List<double>> groupedAreas = new Dictionary<string, List<double>>();
        public bool lowerAlpha = false;

        string lastLocation;

        public List<string> ProcessVecFile(string vec_filePath, double Dt, string dbaseFile, string aep = "", string dur = "", string ens = "")
        {
            List<string> orderOfProcessing = new List<string>();
            try
            {
                string lastCatch = "";
                using (StreamReader reader = new StreamReader(vec_filePath, Encoding.UTF8))
                {
                    List<List<string>> subsToRoute = new List<List<string>>();
                    subsToRoute.Add(new List<string>());
                    string line;
                    int storageLines = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] delim_row = line.Split(' ');
                        string? rowId = null;
                        if (storageLines > 0)
                        {
                            vecInfo[lastCatch].sq[vecInfo[lastCatch].sq.Count - 1].Item1.Add((Convert.ToDouble(delim_row[0]), Convert.ToDouble(delim_row[delim_row.Length - 1])));
                            storageLines -= 1;
                            continue;
                        }
                        for (int i = 0; i < delim_row.Length; i++)
                        {

                            try
                            {
                                if (delim_row[i][0] == '#')
                                {
                                    rowId = delim_row[i].Substring(1);
                                    if (!vecInfo.TryGetValue(rowId, out VecSubby existingValue))
                                    {
                                        vecInfo.Add(rowId, new VecSubby());
                                    }
                                    vecInfo[rowId].Id = rowId;
                                    lastCatch = rowId;
                                }
                            }
                            catch (Exception)
                            {

                            }

                        }
                        if (delim_row[0] == "RAIN" || delim_row[0] == "ADD")
                        {
                            subsToRoute[subsToRoute.Count - 1].Add(rowId);
                            orderOfProcessing.Add(rowId);
                        }
                        else if (delim_row[0] == "STORE.")
                        {
                            subsToRoute.Add(new List<string>());
                        }
                        else if (delim_row[0] == "GET.")
                        {
                            List<string> lastList = subsToRoute[subsToRoute.Count - 1];
                            List<string> secondLastList = subsToRoute[subsToRoute.Count - 2];

                            secondLastList.AddRange(lastList); // Merge the last two lists

                            subsToRoute.RemoveAt(subsToRoute.Count - 1); // Remove the last list
                        }
                        else if (delim_row[0] == "ROUTE")
                        {
                            vecInfo[rowId].UpstreamSubcatchments.AddRange(subsToRoute[subsToRoute.Count - 1]);
                            subsToRoute.RemoveAt(subsToRoute.Count - 1); // Remove the last list
                            subsToRoute.Add(new List<string>());
                        }
                        else if (delim_row[0] == "LOCATION.")
                        {
                            lastLocation = delim_row[1];
                        }
                        else if (delim_row[0] == "DAM")
                        {
                            string lastInfo = "";
                            for (int i = 1; i < delim_row.Length; i++)
                            {
                                if (delim_row[i] != "=")
                                {
                                    if (lastInfo == "")
                                    {
                                        lastInfo = delim_row[i];
                                    }
                                    else if (lastInfo == "VBF")
                                    {
                                        vecInfo[lastCatch].VBF = Convert.ToDouble(delim_row[i]);
                                        lastInfo = "";
                                    }
                                    else if (lastInfo == "FILE")
                                    {
                                        vecInfo[lastCatch].sq.Add((getStorageDischargeFile(delim_row[i]), 0)); // want to send the text after the = sign;
                                        lastInfo = "";
                                    }
                                    else if (lastInfo == "NUMBER")
                                    {
                                        vecInfo[lastCatch].sq.Add((new List<(double, double)>(), 0));
                                        storageLines = Convert.ToInt32(delim_row[i]);
                                        lastInfo = "";
                                    }
                                    lastInfo = delim_row[i];
                                }
                            }
                        }
                    }


                    using (StreamReader dbaseReader = new StreamReader(dbaseFile, Encoding.UTF8))
                    {
                        Dictionary<string, int> colIndex = new Dictionary<string, int>();
                        bool firstRow = true;
                        while ((line = dbaseReader.ReadLine()) != null)
                        {
                            string[] delim_row = line.Split(",");
                            if (firstRow)
                            {
                                if (delim_row[0][0] == '!') { continue; }
                                for (int i = 0; i < delim_row.Length; i++)
                                {
                                    colIndex[delim_row[i]] = i;
                                }
                                firstRow = false;
                            }
                            else
                            {
                                string timeCol = delim_row[colIndex["Column 1"]].Replace("~AEP~", aep).Replace("~DUR~", dur).Replace("~TP~", ens);
                                string valCol = delim_row[colIndex["Column 2"]].Replace("~AEP~", aep).Replace("~DUR~", dur).Replace("~TP~", ens);
                                double addColTime = 0;
                                if (delim_row[colIndex["Add Col 1"]].Length > 0)
                                {
                                    addColTime = Convert.ToDouble(delim_row[colIndex["Add Col 1"]]);
                                }
                                double multColVal = 1;
                                if (delim_row[colIndex["Mult Col 2"]].Length > 0)
                                {
                                    multColVal = Convert.ToDouble(delim_row[colIndex["Mult Col 2"]]);
                                }
                                double addColVal = 0;
                                if (delim_row[colIndex["Add Col 2"]].Length > 0)
                                {
                                    addColVal = Convert.ToDouble(delim_row[colIndex["Add Col 2"]]);
                                }

                                string filePath = Path.Combine(Path.GetDirectoryName(dbaseFile), delim_row[colIndex["Source"]].Replace("\\", "\\\\").Replace("~AEP~", aep).Replace("~DUR~", dur).Replace("~TP~", ens));

                                List<double> groupedRainfall = getDesignRainfall(filePath, Dt, timeCol, valCol, addColTime, multColVal, addColVal); // Catchments with temporal pattern

                                if (delim_row[colIndex["SubCat"]] == "*" || delim_row[colIndex["SubCat"]].ToLower() == "all")
                                {
                                    foreach (string subCat in orderOfProcessing)
                                    {
                                        groupedAreas[subCat] = groupedRainfall;
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        groupedAreas[delim_row[colIndex["SubCat"]]] = groupedRainfall;
                                    }
                                    catch
                                    {

                                        continue;
                                    }
                                }
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured: " + ex.Message);
            }

            return orderOfProcessing;
        }

        public List<double> getDesignRainfall(string filePath, double Dt, string timeCol, string valCol, double addColTime, double multColVal, double addColVal)
        {
            List<double> rainfall = new List<double>();
            // Console.WriteLine(filePath);
            using (StreamReader dbaseReader = new StreamReader(filePath, Encoding.UTF8))
            {
                Dictionary<string, int> colIndex = new Dictionary<string, int>();
                bool firstRow = true;
                double lastTime = 0;
                string line;
                double accRain = 0D;
                while ((line = dbaseReader.ReadLine()) != null)
                {
                    // Console.WriteLine(line);
                    string[] delim_row = line.Split(',');
                    if (firstRow)
                    {
                        if (delim_row.Length > 0)
                        {
                            if (delim_row[0][0] == '!')
                            {
                                continue;
                            }
                        }
                        for (int i = 0; i < delim_row.Length; i++)
                        {
                            colIndex[delim_row[i]] = i;
                        }
                        firstRow = false;
                    }
                    else
                    {

                        double time = (Convert.ToDouble(delim_row[colIndex[timeCol]]) + addColTime)*3600;
                        double timeIncrement = time - lastTime;
                        double rainVal = Convert.ToDouble(delim_row[colIndex[valCol]]) * multColVal + addColVal;
                        if (time < 0)
                        {
                            continue;
                        }
                        else if (time == 0)
                        {
                            rainfall.Add(Convert.ToDouble(delim_row[colIndex[valCol]]) * multColVal + addColVal);
                        }
                        else if (timeIncrement - Dt > timeIncrement * 0.05) // Bigger Data interval than timestep
                        {
                            double interpolationTimes = timeIncrement / Dt;
                            double totalRain = rainVal + accRain;
                            double rainPerTimeStep = totalRain / interpolationTimes;

                            for (int i = 0; i < (int)interpolationTimes; i++)
                            {
                                rainfall.Add(rainPerTimeStep);
                            }
                            accRain = (interpolationTimes - ((int)interpolationTimes)) * rainPerTimeStep;
                            lastTime = time + (interpolationTimes - ((int)interpolationTimes)) * Dt;
                        }
                        else if (timeIncrement - Dt > -timeIncrement * 0.05) // On Timestep
                        {
                            rainfall.Add(rainVal + accRain);
                            accRain = 0D;
                            lastTime = time;
                        }
                        else // If timestep is less than Dt
                        {
                            accRain += rainVal;
                        }
                    }
                }
            }
            //int desiredLength = (int) (rainfall.Count * 2 + 6 / Dt);
            int desiredLength = (int)(rainfall.Count * 1.5 + 3600 * 6 / Dt);
            int zerosToAdd = desiredLength - rainfall.Count;
            for (int i = 0; i < zerosToAdd; i++)
            {
                rainfall.Add(0);
            }
            // Console.WriteLine(rainfall.Count);
            return rainfall;
        }

        public List<(double, double)> getStorageDischargeFile(string fileName)
        {
            List<(double, double)> storagePairs = new List<(double, double)>();
            using (TextFieldParser parser = new TextFieldParser(fileName))
            {
                // Set the delimiter for the CSV file
                parser.Delimiters = new string[] { " ", "\t", "\r" };
                int countLines = 1;
                while (!parser.EndOfData)
                {

                    List<string> fields = parser.ReadFields().ToList();
                    if (fields.Count > 1 && countLines > 2)
                    {
                        storagePairs.Add((Convert.ToDouble(fields[0]), Convert.ToDouble(fields[fields.Count - 1])));
                    }
                    countLines++;
                }
            }
            return storagePairs;
        }

        public void ProcessCatFile(string cat_filePath)
        {
            Dictionary<string, int> charIndicies = new Dictionary<string, int>();
            using (StreamReader reader = new StreamReader(cat_filePath, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] delim_row = line.Split(',');

                    if (delim_row[0] == "Index")
                    {
                        string[] columnNames = { "Area", "L", "Sc", "N", "HL", "HS", "k", "d", "I", "UL", "UM", "UH", "UF" };
                        for (int i = 0; i <= delim_row.Length - 1; i++)
                        {
                            if (columnNames.Contains(delim_row[i]))
                            {
                                charIndicies.Add(delim_row[i], i);
                            }
                        }
                    }
                    else
                    {
                        CatSubby catSubby = new CatSubby();
                        catSubby.Id = delim_row[0];
                        try
                        {
                            catSubby.Area = double.Parse(delim_row[charIndicies["Area"]]) * 1000000;
                            catSubby.L = Convert.ToDouble(delim_row[charIndicies["L"]]) * 1000;
                            catSubby.Sc = double.Parse(delim_row[charIndicies["Sc"]]);
                            catSubby.N = double.Parse(delim_row[charIndicies["N"]]);
                            catSubby.HL = double.Parse(delim_row[charIndicies["HL"]]) * 1000;
                            catSubby.HS = double.Parse(delim_row[charIndicies["HS"]]);
                        }
                        catch
                        {
                            Console.WriteLine($"Incomplete Catchment Data for ID: {catSubby.Id}");
                            Environment.Exit(0);
                        }



                        try { catSubby.F = double.Parse(delim_row[charIndicies["UF"]]); } catch { catSubby.F = 0; }

                        try
                        {
                            double ULI = double.Parse(delim_row[charIndicies["UL"]]) * Math.Min(0.17 / 0.5, 1);
                            catSubby.U = ULI;
                        }
                        catch
                        { }
                        try
                        {
                            double UMI = double.Parse(delim_row[charIndicies["UM"]]) * Math.Min(0.33 / 0.5, 1);
                            catSubby.U = catSubby.U + UMI;
                        }
                        catch
                        { }
                        try
                        {
                            double UHI = double.Parse(delim_row[charIndicies["UH"]]) * Math.Min(0.5 / 0.5, 1);
                            catSubby.U = Math.Min((double)catSubby.U + UHI, 1);
                        }
                        catch
                        { }
                        try { catSubby.kappa = double.Parse(delim_row[charIndicies["k"]]); } catch { catSubby.kappa = 0; }
                        try { catSubby.delta = double.Parse(delim_row[charIndicies["d"]]); } catch { catSubby.delta = 0; }

                        if (catSubby.I == 0)
                        {
                            try
                            {
                                catSubby.I = catSubby.I + catSubby.U / 2;
                            }
                            catch
                            {
                                catSubby.I = 0;
                            }
                        }

                        try { catSubby.I = double.Parse(delim_row[charIndicies["I"]]); } catch { }
                        catInfo.Add(catSubby.Id, catSubby);
                    }
                }
            }
        }



    }
}

public class fileLocations
{
    public string catchmentFile;
    public string vectorFile;
    public Dictionary<List<string>, double> prebursts = new Dictionary<List<string>, double>();
    public List<string> aeps;
    public List<string> durations;
    public List<string> ensembles;
    public string dbaseFile = "";
    public string recordedFlows;
    public string run;
    public string fitsFileName = "BestFits.csv";
    public string outputFile = "Output.csv";
    public string outputDir = "Outputs";
    public string IL;
    public double CL;
    public double X = 0.1;
    public double N = 1;
    public double alpha = 0;
    public double beta = 0;
    public double m = 0;
    public string rdf;

    public fileLocations(string paramFile)
    {
        using (TextFieldParser parser = new TextFieldParser(paramFile))
        {
            // Set the delimiter for the CSV file
            parser.Delimiters = new string[] { "=", " ", "," };

            // Read the fields while the end of the file is not reached
            while (!parser.EndOfData)
            {
                List<string> fields = parser.ReadFields().ToList();
                if (fields.Count > 0)
                {
                    if (fields[0].ToLower() == "cat")
                    {
                        catchmentFile = fields[fields.Count - 1].Replace("\\", "\\\\");
                    }
                    else if (fields[0].ToLower() == "run")
                    {
                        run = fields[fields.Count - 1].Replace("\\", "\\\\");
                    }
                    else if (fields[0].ToLower() == "vec")
                    {
                        vectorFile = fields[fields.Count - 1].Replace("\\", "\\\\");
                    }
                    else if (fields[0].ToLower() == "cal")
                    {
                        recordedFlows = fields[fields.Count - 1].Replace("\\", "\\\\");
                    }
                    else if (fields[0].ToLower() == "fits")
                    {
                        fitsFileName = fields[fields.Count - 1].Replace("\\", "\\\\");
                    }
                    else if (fields[0].ToLower() == "output")
                    {
                        outputFile = fields[fields.Count - 1].Replace("\\", "\\\\");
                    }
                    else if (fields[0].ToUpper() == "IL")
                    {
                        IL = fields[fields.Count - 1];
                    }
                    else if (fields[0].ToUpper() == "CL")
                    {
                        CL = Convert.ToDouble(fields[fields.Count - 1]);
                    }
                    else if (fields[0].ToUpper() == "N")
                    {
                        N = Convert.ToDouble(fields[fields.Count - 1]);
                    }
                    else if (fields[0].ToUpper() == "X")
                    {
                        X = Convert.ToDouble(fields[fields.Count - 1]);
                    }
                    else if (fields[0].ToLower() == "dbase")
                    {
                        dbaseFile = fields[fields.Count - 1].Replace("\\", "\\\\");
                    }
                    else if (fields[0].ToLower() == "aep" || fields[0].ToLower() == "aeps" || fields[0].ToLower() == "ari" || fields[0].ToLower() == "aris")
                    {
                        aeps = new List<string>() { };
                        foreach (string col in fields.GetRange(1, fields.Count - 1))
                        {
                            if (col.Length > 0)
                            {
                                aeps.Add(col);
                            }
                        }

                    }
                    else if (fields[0].ToLower() == "dur" || fields[0].ToLower() == "durs" || fields[0].ToLower() == "duration" || fields[0].ToLower() == "durations")
                    {
                        durations = new List<string>() { };
                        foreach (string col in fields.GetRange(1, fields.Count - 1))
                        {
                            if (col.Length > 0)
                            {
                                durations.Add(col);
                            }
                        }
                    }
                    else if (fields[0].ToLower() == "ensemble" || fields[0].ToLower() == "ensembles" || fields[0].ToLower() == "tp" || fields[0].ToLower() == "tps")
                    {
                        ensembles = new List<string>() { };
                        foreach (string col in fields.GetRange(1, fields.Count - 1))
                        {
                            if (col.Length > 0)
                            {
                                ensembles.Add(col);
                            }
                        }
                    }
                    else if (fields[0].ToLower() == "outputdir")
                    {
                        outputDir = fields[fields.Count - 1].Replace("\\", "\\\\");
                    }
                    else if (fields[0].ToLower() == "a")
                    {
                        alpha = Convert.ToDouble(fields[fields.Count - 1]);
                    }
                    else if (fields[0].ToLower() == "b")
                    {
                        beta = Convert.ToDouble(fields[fields.Count - 1]);
                    }
                    else if (fields[0].ToLower() == "m")
                    {
                        m = Convert.ToDouble(fields[fields.Count - 1]);
                    }
                    else if (fields[0] == "!")
                    {
                        continue;
                    }
                    else
                    {
                        Console.Write($"Unrecognized Command in Parameter File: {fields[0]}");
                        Environment.Exit(0);
                    }
                }
            }
        }
    }
}
