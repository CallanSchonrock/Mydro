using System;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Xml;

namespace Transput_Handler
{
    public class VecSubby
    {
        public string? Id;
        public List<string> UpstreamSubcatchments = new List<string>();
        public List<(List<(double, double)>, double)> sq = new List<(List<(double, double)>, double)>();
        public List<(List<(double, double)>, double)> sa = new List<(List<(double, double)>, double)>();
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
        public string? Id;
        public double L, Area, Sc, N, HL, HS, I, U, F, kappa = 0, delta = 0;
    }

    class getCatch_var
    {
        public Dictionary<string, VecSubby> vecInfo = new Dictionary<string, VecSubby>();
        public Dictionary<string, CatSubby> catInfo = new Dictionary<string, CatSubby>();
        public Dictionary<string, List<double>> groupedAreas = new Dictionary<string, List<double>>();
        public bool lowerAlpha = false;

        string? lastLocation;

        public List<string> ProcessVecFile(string vec_filePath, double Dt, string rf_dbaseFile, string sq_dbaseFile = "", string sa_dbaseFile = "", string aep = "", string dur = "", string ens = "")
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
                    int countLines = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        countLines++;
                        try
                        {
                            string[] delim_row = line.Split(new char[] { ',', ' ', '\t' });
                            // Find the index of the word containing the exclamation mark
                            int indexOfExclamation = -1;
                            for (int i = 0; i < delim_row.Length; i++)
                            {
                                if (delim_row[i].Contains('!'))
                                {
                                    indexOfExclamation = i;
                                    break;
                                }
                            }

                            if (indexOfExclamation != -1)
                            {
                                Array.Resize(ref delim_row, indexOfExclamation); // Resize the array to exclude elements after "!"
                            }
                            string? rowId = null;
                            if (storageLines > 0)
                            {
                                vecInfo[lastCatch].sq[vecInfo[lastCatch].sq.Count - 1].Item1.Add((Convert.ToDouble(delim_row[0]) * 1000, Convert.ToDouble(delim_row[delim_row.Length - 1])));
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
                            if (delim_row.Contains("RAIN") || delim_row.Contains("ADD"))
                            {
                                subsToRoute[subsToRoute.Count - 1].Add(rowId);
                                orderOfProcessing.Add(rowId);
                            }
                            else if (delim_row.Contains("STORE."))
                            {
                                subsToRoute.Add(new List<string>());
                            }
                            else if (delim_row.Contains("GET."))
                            {
                                List<string> lastList = subsToRoute[subsToRoute.Count - 1];
                                List<string> secondLastList = subsToRoute[subsToRoute.Count - 2];

                                secondLastList.AddRange(lastList); // Merge the last two lists

                                subsToRoute.RemoveAt(subsToRoute.Count - 1); // Remove the last list
                            }
                            else if (delim_row.Contains("ROUTE"))
                            {
                                vecInfo[rowId].UpstreamSubcatchments.AddRange(subsToRoute[subsToRoute.Count - 1]);
                                subsToRoute.RemoveAt(subsToRoute.Count - 1); // Remove the last list
                                subsToRoute.Add(new List<string>());
                            }
                            
                            
                        }catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            Console.WriteLine($"Error on line {countLines} of Routing file {vec_filePath}");
                        }
                    }
                    if (sq_dbaseFile != "")
                    {
                        Dictionary<string, (List<double>, List<double>, double, double)> sqs = dbaseHandler(sq_dbaseFile);
                        foreach (var pair in sqs)
                        {
                            vecInfo[pair.Key].sq.Add((pair.Value.Item1.Zip(pair.Value.Item2, (x, y) => (x, y)).ToList(), pair.Value.Item3));
                        }
                    }
                    if (sa_dbaseFile != "")
                    {
                        Dictionary<string, (List<double>, List<double>, double, double)> sas = dbaseHandler(sa_dbaseFile);
                        foreach (var pair in sas)
                        {
                            vecInfo[pair.Key].sa.Add((pair.Value.Item1.Zip(pair.Value.Item2, (x, y) => (x, y)).ToList(), pair.Value.Item3));
                        }
                    }

                    using (StreamReader dbaseReader = new StreamReader(rf_dbaseFile, Encoding.UTF8))
                    {
                        Dictionary<string, int> colIndex = new Dictionary<string, int>();
                        bool firstRow = true;
                        while ((line = dbaseReader.ReadLine()) != null)
                        {
                            string[] delim_row = line.Split(new char[] { ',', '\t' });
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

                                string filePath = Path.Combine(Path.GetDirectoryName(rf_dbaseFile), delim_row[colIndex["Source"]].Replace("\\", "\\\\").Replace("~AEP~", aep).Replace("~DUR~", dur).Replace("~TP~", ens));

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
        public Dictionary<string, (List<double>, List<double>, double, double)> dbaseHandler(string filePath)
        {
            /*
            Returns:    Source: Column1, Column2, Column3, Column4                        
            */
            Dictionary<string, (List<double>, List<double>, double, double)> dbaseData = new Dictionary<string, (List<double>, List<double>, double, double)>();
            string line;
            using (StreamReader dbaseReader = new StreamReader(filePath))
            {
                bool header = true;
                while ((line = dbaseReader.ReadLine()) != null)
                {
                    
                    if (header) { header = false;  continue; }
                    int index = line.IndexOf('!');
                    if (index != -1)
                    {
                        line = line.Substring(0, index);
                    }
                    if (line.Length < 1) { continue; }
                    string[] delim_row = line.Split(new char[] { ',', '\t' });
                    string subcat = delim_row[0];
                    string source = delim_row[1];
                    List<double> col1 = new List<double>(), col2 = new List<double>();
                    double addCol1 = 0, multCol2 = 1, addCol2 = 0 ;
                    if (double.TryParse(delim_row[4], out _))
                    {
                        addCol1 = double.Parse(delim_row[4]);
                    }
                    if (double.TryParse(delim_row[5], out _))
                    {
                        multCol2 = double.Parse(delim_row[5]);
                    }
                    if (double.TryParse(delim_row[6], out _))
                    {
                        addCol2 = double.Parse(delim_row[6]);
                    }
                    double col3 = 0, col4 = 0;
                    if (source.Length > 0)
                    {
                        using (StreamReader csvReader = new StreamReader(source))
                        {
                            string csv_line;
                            bool csvHeader = true;
                            int indexCol = -1;
                            int valCol = -1;
                            while ((csv_line = csvReader.ReadLine()) != null)
                            {
                                string[] values = csv_line.Split(new char[] { ',', '\t' });
                                if (csvHeader)
                                {
                                    for (int col = 0; col < values.Length; col++)
                                    {
                                        if (values[col] == delim_row[2]) { indexCol = col; }
                                        if (values[col] == delim_row[3]) { valCol = col; }
                                    }
                                    if (indexCol == -1 || valCol == -1)
                                    {
                                        Console.WriteLine($"ERROR: Could not find column/s {col1[0]}, {col2[0]} in csv {source}");
                                        Environment.Exit(0);
                                    }
                                    csvHeader = false;
                                    continue;
                                }
                                if (values[indexCol].Length > 0 && values[valCol].Length > 0)
                                {
                                    try
                                    {
                                        col1.Add(double.Parse(values[indexCol]) + addCol1);
                                        col2.Add(double.Parse(values[valCol]) * multCol2 + addCol2);
                                    }
                                    catch
                                    {
                                        Console.WriteLine($"ERROR: Cannot Interpret Value as float in column/s {col1[0]}, {col2[0]} in csv {source}");
                                        Environment.Exit(0);
                                    }
                                }
                                else
                                {
                                    continue;
                                }
                            }

                        }
                    }
                    else
                    {
                        try
                        {
                            col1.Add(float.Parse(delim_row[2]));
                        }
                        catch { }
                        try
                        {
                            col2.Add(float.Parse(delim_row[3]));
                        }
                        catch { }
                    }
                    try
                    {
                        col3 = float.Parse(delim_row[7]);
                    }
                    catch { }
                    try
                    {
                        col4 = float.Parse(delim_row[8]);
                    }
                    catch { }
                    dbaseData.Add(subcat, (col1, col2, col3, col4));
                }
            }
            return dbaseData;
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
                    string[] delim_row = line.Split(new char[] { ',', '\t' });
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
                // Set the delimiter for the CSV files
                parser.Delimiters = new string[] { ",", "\t" };
                int countLines = 1;
                while (!parser.EndOfData)
                {

                    List<string> fields = parser.ReadFields().ToList();
                    if (fields.Count > 1 && countLines > 2)
                    {
                        string? storage = null;
                        string? discharge = null;
                        foreach (string field in fields)
                        {
                            if (field.Contains("Storage"))
                            {
                                continue;
                            }
                            if (storage == null)
                            {
                                if (!string.IsNullOrEmpty(field)) { storage = field; } // MEGALITRE
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(field)) { discharge = field; break; }
                            }
                        }
                        try
                        {
                            storagePairs.Add((Convert.ToDouble(storage) * 1000, Convert.ToDouble(discharge))); // Conversion to Cubic metres
                        }catch (Exception ex)
                        {
                            Console.WriteLine($"Error in Storage Discharge File {fileName} Line: {countLines}");
                        }
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
                    string[] delim_row = line.Split(new char[] { ',', '\t' });

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
    public string rf_dbaseFile = "", sq_dbaseFile = "", sa_dbaseFile = "";
    public string recordedFlows;
    public string run;
    public string fitsFileName = "BestFits.csv";
    public string outputFile = "Output.csv";
    public string outputDir = "Outputs";
    public string IL = "0";
    public double CL = 0;
    public double X = 0.1;
    public double N = 1;
    public double alpha = 0;
    public double beta = 0;
    public double m = 0;
    public string rdf;
    public double ilRecoveryRate = 0; // mm/hr

    public fileLocations(string paramFile)
    {
        using (TextFieldParser parser = new TextFieldParser(paramFile))
        {
            // Set the delimiter for the CSV file
            parser.Delimiters = new string[] { "=", "," };
            int rowCount = 0;
            // Read the fields while the end of the file is not reached
            while (!parser.EndOfData)
            {
                rowCount++;
                List<string> fields = parser.ReadFields().ToList();

                if (fields.Count > 1)
                {
                    if (fields[0].Trim().ToLower() == "cat")
                    {
                        catchmentFile = fields[1].Trim().Replace("\\", "\\\\");
                    }
                    else if (fields[0].Trim().ToLower() == "run")
                    {
                        run = fields[1].Trim().Replace("\\", "\\\\");
                    }
                    else if (fields[0].Trim().ToLower() == "vec")
                    {
                        vectorFile = fields[1].Trim().Replace("\\", "\\\\");
                    }
                    else if (fields[0].Trim().ToLower() == "cal")
                    {
                        recordedFlows = fields[1].Trim().Replace("\\", "\\\\");
                    }
                    else if (fields[0].Trim().ToLower() == "fits")
                    {
                        fitsFileName = fields[1].Trim().Replace("\\", "\\\\");
                    }
                    else if (fields[0].Trim().ToLower() == "output")
                    {
                        outputFile = fields[1].Trim().Replace("\\", "\\\\");
                    }
                    else if (fields[0].Trim().ToUpper() == "IL")
                    {
                        IL = fields[1].Trim();
                    }
                    else if (fields[0].Trim().ToUpper() == "CL")
                    {
                        CL = Convert.ToDouble(fields[1].Trim());
                    }
                    else if (fields[0].Trim().ToUpper() == "N")
                    {
                        N = Convert.ToDouble(fields[1].Trim());
                    }
                    else if (fields[0].Trim().ToUpper() == "X")
                    {
                        X = Convert.ToDouble(fields[1].Trim());
                    }
                    else if (fields[0].Trim().ToLower() == "sq_dbase")
                    {
                        sq_dbaseFile = fields[1].Trim().Replace("\\", "\\\\");
                    }
                    else if (fields[0].Trim().ToLower() == "sa_dbase")
                    {
                        sa_dbaseFile = fields[1].Trim().Replace("\\", "\\\\");
                    }
                    else if (fields[0].Trim().ToLower() == "rf_dbase")
                    {
                        rf_dbaseFile = fields[1].Trim().Replace("\\", "\\\\");
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
                    else if (fields[0].Trim().ToLower() == "outputdir")
                    {
                        outputDir = fields[1].Trim().Replace("\\", "\\\\");
                    }
                    else if (fields[0].Trim().ToLower() == "a")
                    {
                        alpha = Convert.ToDouble(fields[1].Trim());
                    }
                    else if (fields[0].Trim().ToLower() == "b")
                    {
                        beta = Convert.ToDouble(fields[1].Trim());
                    }
                    else if (fields[0].Trim().ToLower() == "m")
                    {
                        m = Convert.ToDouble(fields[1].Trim());
                    } 
                    else if (fields[0].Trim().ToLower() == "il recovery rate")
                    {
                        ilRecoveryRate = Convert.ToDouble(fields[1].Trim()) / 3600;
                    }
                    else if (fields[0][0] == '!')
                    {
                        continue;
                    }
                    else
                    {
                        Console.Write($"Unrecognized Command in Parameter File on line: {rowCount}: {fields[0]}");
                        Environment.Exit(0);
                    }
                }
            }
        }
    }
}
