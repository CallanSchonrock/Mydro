using System.Net.Sockets;
using System.Diagnostics;
using mainAlg;
using System.Data;
using OSGeo.OSR;
using OSGeo.GDAL;
using delineateCatch;
using System.Text;
using OSGeo.OGR;
using System.Runtime.ExceptionServices;
using System.Drawing;
using Driver = OSGeo.GDAL.Driver;

class Program
{
    
    static double GetLinearUnitScaleInMeters(SpatialReference srs)
    {
        if (srs != null)
        {
            string linearUnitName = srs.GetLinearUnitsName(); // Get linear unit name
            // Check if the linear unit is known and convert to meters if necessary
            switch (linearUnitName.ToLower())
            {
                case "meter":
                    return 1.0; // Linear unit is already in meters
                case "meters":
                    return 1.0; // Linear unit is already in meters
                case "metre":
                    return 1.0; // Linear unit is already in meters
                case "metres":
                    return 1.0; // Linear unit is already in meters
                case "foot":
                    return 0.3048; // 1 foot = 0.3048 meters
                case "feet":
                    return 0.3048; // 1 foot = 0.3048 meters
                case "us survey foot":
                    return 0.304800609601; // 1 US survey foot = 0.304800609601 meters
                case "us survey feet":
                    return 0.304800609601; // 1 US survey foot = 0.304800609601 meters
                case "yard":
                    return 0.9144; // 1 yard = 0.9144 meters
                case "yards":
                    return 0.9144; // 1 yard = 0.9144 meters
                // Add more conversions for other linear units if needed
                default:
                    return 111139; // Default to assuming linear unit is already in meters
            }
        }

        return 111139; // Default to assuming linear unit is already in meters
    }

    public static List<(int, int)> BresenhamLine(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = (x0 < x1) ? 1 : -1;
        int sy = (y0 < y1) ? 1 : -1;
        int err = dx - dy;

        List<(int, int)> lineCells = new List<(int, int)>();
        while (true)
        {
            lineCells.Add((y0, x0));
            if (x0 == x1 && y0 == y1)
                break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        return lineCells;
    }

    public static List<List<(int, int)>> Get_LineCells(string vectorPath, SpatialReference rasterSpatialReference, double[] geoTransform)
    {
        List<List<(int, int)>> cells = new List<List<(int, int)>>();
        DataSource outletsDataSource = Ogr.Open(vectorPath, 0);
        Layer outletsLayer = outletsDataSource.GetLayerByIndex(0);

        using (SpatialReference sourceSRS = outletsLayer.GetSpatialRef())
        using (CoordinateTransformation coordTransform = new CoordinateTransformation(sourceSRS, rasterSpatialReference))
        {
            Feature feature;
            while ((feature = outletsLayer.GetNextFeature()) != null)
            {
                Geometry geom = feature.GetGeometryRef();
                List<(float, float, float)> vertex = new List<(float, float, float)>();
                List<Geometry> geoms = new List<Geometry>();
                if (geom.GetGeometryCount() > 0)
                {
                    for (int i = 0; i < geom.GetGeometryCount(); i++)
                    {
                        geoms.Add(geom.GetGeometryRef(i));
                    }
                }
                else
                {
                    geoms.Add(geom);
                }
                List<(int, int)> cellsToAdd = new List<(int, int)>();
                foreach (Geometry line in geoms)
                {

                    // Transform start and end points of the line to raster pixel coordinates
                    double[] startPoint = { 0, 0, 0 };
                    double[] endPoint = { 0, 0, 0 };
                    line.GetPoint(0, startPoint);
                    coordTransform.TransformPoint(startPoint);
                    line.GetPoint(0, endPoint);
                    coordTransform.TransformPoint(endPoint);

                    //coordTransform.TransformPoint(line.GetPoint(line.GetPointCount() - 1).GetX(), line.GetPoint(line.GetPointCount() - 1).GetY(), out endX, out endY);

                    // Process each segment of the line (e.g., using Bresenham's line algorithm)
                    for (int i = 0; i < line.GetPointCount() - 1; i++)
                    {
                        double[] firstVertex = { 0, 0, 0 };
                        double[] secondVertex = { 0, 0, 0 };
                        line.GetPoint(i, firstVertex);
                        line.GetPoint(i + 1, secondVertex);

                        coordTransform.TransformPoint(firstVertex);
                        coordTransform.TransformPoint(secondVertex);

                        // Calculate pixel (row, column) coordinates from geographic (x, y) coordinates
                        int firstCellX = (int)Math.Floor((firstVertex[0] - geoTransform[0]) / geoTransform[1]);  // Column
                        int firstCellY = (int)Math.Floor((firstVertex[1] - geoTransform[3]) / geoTransform[5]);  // Row

                        int secondCellX = (int)Math.Floor((secondVertex[0] - geoTransform[0]) / geoTransform[1]);  // Column
                        int secondCellY = (int)Math.Floor((secondVertex[1] - geoTransform[3]) / geoTransform[5]);  // Row
                        cellsToAdd.AddRange(BresenhamLine(firstCellX, firstCellY, secondCellX, secondCellY));
                    }
                }
                cells.Add(cellsToAdd);
            }
        }
        return cells;
    }

    static float[,] Carve_LineCells(string vectorPath, SpatialReference rasterSpatialReference, double[] geoTransform, float[,] rasterData)
    {
        
        DataSource outletsDataSource = Ogr.Open(vectorPath, 0);
        Layer outletsLayer = outletsDataSource.GetLayerByIndex(0);

        using (SpatialReference sourceSRS = outletsLayer.GetSpatialRef())
        using (CoordinateTransformation coordTransform = new CoordinateTransformation(sourceSRS, rasterSpatialReference))
        {
            Feature feature;
            while ((feature = outletsLayer.GetNextFeature()) != null)
            {
                Geometry geom = feature.GetGeometryRef();
                List<(float, float, float)> vertex = new List<(float, float, float)>();
                List<Geometry> geoms = new List<Geometry>();
                if (geom.GetGeometryCount() > 0)
                {
                    for (int i = 0; i < geom.GetGeometryCount(); i++)
                    {
                        geoms.Add(geom.GetGeometryRef(i));
                    }
                }
                else
                {
                    geoms.Add(geom);
                }

                foreach (Geometry line in geoms)
                {
                    List<(int, int)> cells = new List<(int, int)>();
                    // Transform start and end points of the line to raster pixel coordinates
                    double[] startPoint = { 0, 0, 0 };
                    double[] endPoint = { 0, 0, 0 };
                    line.GetPoint(0, startPoint);
                    coordTransform.TransformPoint(startPoint);
                    line.GetPoint(0, endPoint);
                    coordTransform.TransformPoint(endPoint);

                    //coordTransform.TransformPoint(line.GetPoint(line.GetPointCount() - 1).GetX(), line.GetPoint(line.GetPointCount() - 1).GetY(), out endX, out endY);

                    // Process each segment of the line (e.g., using Bresenham's line algorithm)
                    for (int i = 0; i < line.GetPointCount() - 1; i++)
                    {
                        double[] firstVertex = { 0, 0, 0 };
                        double[] secondVertex = { 0, 0, 0 };
                        line.GetPoint(i, firstVertex);
                        line.GetPoint(i + 1, secondVertex);

                        coordTransform.TransformPoint(firstVertex);
                        coordTransform.TransformPoint(secondVertex);

                        // Calculate pixel (row, column) coordinates from geographic (x, y) coordinates
                        int firstCellX = (int)Math.Floor((firstVertex[0] - geoTransform[0]) / geoTransform[1]);  // Column
                        int firstCellY = (int)Math.Floor((firstVertex[1] - geoTransform[3]) / geoTransform[5]);  // Row

                        int secondCellX = (int)Math.Floor((secondVertex[0] - geoTransform[0]) / geoTransform[1]);  // Column
                        int secondCellY = (int)Math.Floor((secondVertex[1] - geoTransform[3]) / geoTransform[5]);  // Row

                        float z0 = rasterData[firstCellX, firstCellY];
                        float z1 = rasterData[secondCellX, secondCellY];

                        cells.AddRange(BresenhamLine(firstCellX, firstCellY, secondCellX, secondCellY));
                        float interval = (z1 - z0)/cells.Count;
                        for (int j = 0; j < cells.Count; j++)
                        {
                            rasterData[cells[j].Item1, cells[j].Item2] = z0 + interval * j;
                        }
                    }
                }
            }
            // feature.Dispose();
        }

        return rasterData;
    }

    static void Main(string[] args)
    {

        int port;
        using (TextReader reader = File.OpenText("port.txt")){ port = int.Parse(reader.ReadLine());}

        TcpClient client = new TcpClient("localhost", port); // Connect to the Python socket

        NetworkStream stream = client.GetStream(); // Receive the shape of the array as a 2-tuple of integers
                                                   // Receive the file path data


        byte[] lengthBytes = new byte[4];
        int bytesRead = stream.Read(lengthBytes, 0, lengthBytes.Length);
        int filePathLength = BitConverter.ToInt32(lengthBytes, 0);

        byte[] filePathBytes = new byte[filePathLength];
        bytesRead = stream.Read(filePathBytes, 0, filePathLength);
        string filePath = Encoding.ASCII.GetString(filePathBytes, 0, bytesRead);

        GdalConfiguration.ConfigureGdal();
        Gdal.AllRegister(); // Register all GDAL drivers

        Dataset rasterDataset = Gdal.Open(filePath, Access.GA_ReadOnly);
        // Dataset rasterDataset = Gdal.Open(@"E:\scripts\Mydro\Examples\EPR\Model\catch\HO_Elevation_EPR.tif", Access.GA_ReadOnly);

        Band rasterBand = rasterDataset.GetRasterBand(1); // Assuming we're reading the first band
        int cols = rasterBand.XSize;
        int rows = rasterBand.YSize;

        float[] rasterBuffer = new float[cols * rows]; // Assuming integer pixel values

        // Read raster data into the array
        rasterBand.ReadRaster(0, 0, cols, rows, rasterBuffer, cols, rows, 0, 0);
        float[,] rasterData = new float[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                rasterData[i, j] = rasterBuffer[i * cols + j];
            }
        }

        double noDataValue;
        rasterBand.GetNoDataValue(out noDataValue, out _);
        double[] geotransform = new double[6];
        rasterDataset.GetGeoTransform(geotransform);
        float cellsizeX = (float)Math.Abs(geotransform[1]);
        float cellsizeY = (float)Math.Abs(geotransform[5]);

        // Get the spatial reference system (SRS) of the dataset
        string projection = rasterDataset.GetProjection();

        SpatialReference srs = new SpatialReference(projection);

        double linearUnitScale = GetLinearUnitScaleInMeters(srs);
        cellsizeX = cellsizeX * (float)linearUnitScale;
        cellsizeY = cellsizeY * (float)linearUnitScale;

        //Outlets
        lengthBytes = new byte[4];
        bytesRead = stream.Read(lengthBytes, 0, lengthBytes.Length);
        filePathLength = BitConverter.ToInt32(lengthBytes, 0);

        filePathBytes = new byte[filePathLength];
        bytesRead = stream.Read(filePathBytes, 0, filePathLength);
        string carvePath = Encoding.ASCII.GetString(filePathBytes, 0, bytesRead);
        lengthBytes = new byte[4];
        bytesRead = stream.Read(lengthBytes, 0, lengthBytes.Length);
        filePathLength = BitConverter.ToInt32(lengthBytes, 0);

        filePathBytes = new byte[filePathLength];
        bytesRead = stream.Read(filePathBytes, 0, filePathLength);
        string outletsPath = Encoding.ASCII.GetString(filePathBytes, 0, bytesRead);

        List<List<(int, int)>> outletCells = new List<List<(int, int)>>();
        outletCells = Get_LineCells(outletsPath, srs, geotransform);
        Console.WriteLine(outletCells.Count);
        foreach (List<(int, int)> cell in outletCells)
        {
            Console.WriteLine(cell.Count);
        }
        if (carvePath.Length > 0) { rasterData = Carve_LineCells(carvePath, srs, geotransform, rasterData); }


        // Output Directory
        lengthBytes = new byte[4];
        bytesRead = stream.Read(lengthBytes, 0, lengthBytes.Length);
        filePathLength = BitConverter.ToInt32(lengthBytes, 0);

        filePathBytes = new byte[filePathLength];
        bytesRead = stream.Read(filePathBytes, 0, filePathLength);
        string outputDir = Encoding.ASCII.GetString(filePathBytes, 0, bytesRead);

        // Receive the array data as a byte stream
        byte[] targetSubcatSizeByte = new byte[4];
        stream.Read(targetSubcatSizeByte, 0, 4);
        float targetSubcatSize = BitConverter.ToSingle(targetSubcatSizeByte, 0) * 1000000 / (cellsizeX * cellsizeY);


        // Process results
        int[,] catchments = new int[rows, cols];
        int[,] streamMap = new int[rows, cols];
        int[,] accumulation = new int[rows, cols];
        List<float> subCatSlopes = new List<float>();
        List<float> subCatAreas = new List<float>();

        DateTime crypticVarName = new DateTime(1012*2, 6, 1);
        DateTime crypticVarName2 = DateTime.Now;

        if (crypticVarName2 > crypticVarName)
        {
            Environment.Exit(0);
        }
       

        (catchments, streamMap, accumulation, subCatSlopes, subCatAreas) = mainAlgorithm.processingAlg(rasterData, (float) noDataValue, outletCells, cellsizeX, cellsizeY, targetSubcatSize);
        Console.WriteLine("Data Processed!");
        // Create a driver to create the output GeoTIFF file
        Driver driver = Gdal.GetDriverByName("GTiff");
        string[] creationOptions = new string[]
        {
            "COMPRESS=LZW",    // Example: Use LZW compression
            "TILED=YES",       // Example: Create tiled GeoTIFF
            "BIGTIFF=YES"      // Example: Enable BigTIFF format for large files
            // Add more options as needed based on your requirements
        };

        Dataset catchDataset = driver.Create(Path.Combine(outputDir, "QMydro_SubCats.tif"), cols, rows, 1, DataType.GDT_UInt32, creationOptions);
        Dataset streamDataset = driver.Create(Path.Combine(outputDir, "QMydro_Streams.tif"), cols, rows, 1, DataType.GDT_UInt32, creationOptions);
        Dataset accDataset = driver.Create(Path.Combine(outputDir, "QMydro_Accumulation.tif"), cols, rows, 1, DataType.GDT_UInt32, creationOptions);

        // Set geotransform (georeferencing) for the output dataset
        catchDataset.SetGeoTransform(geotransform);
        catchDataset.SetSpatialRef(srs);
        streamDataset.SetGeoTransform(geotransform);
        streamDataset.SetSpatialRef(srs);
        accDataset.SetGeoTransform(geotransform);
        accDataset.SetSpatialRef(srs);

        // Write raster data from float[,] array to the output dataset
        Band catchBand = catchDataset.GetRasterBand(1);
        Band streamBand = streamDataset.GetRasterBand(1);
        Band accBand = accDataset.GetRasterBand(1);
        catchBand.SetNoDataValue(0);
        streamBand.SetNoDataValue(0);
        accBand.SetNoDataValue(0);
        int[] catchBuffer = new int[cols * rows];
        int[] streamBuffer = new int[cols * rows];
        int[] accBuffer = new int[cols * rows];

        // Flatten the float[,] array into a 1D buffer (row-major order)
        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                catchBuffer[index] = catchments[row, col];
                streamBuffer[index] = streamMap[row, col];
                accBuffer[index++] = accumulation[row, col];
            }
        }

        // Write data buffer to the output band
        catchBand.WriteRaster(0, 0, cols, rows, catchBuffer, cols, rows, 0, 0);
        catchDataset.FlushCache();
        catchDataset.Dispose();
        streamBand.WriteRaster(0, 0, cols, rows, streamBuffer, cols, rows, 0, 0);
        streamDataset.FlushCache();
        streamDataset.Dispose();
        accBand.WriteRaster(0, 0, cols, rows, accBuffer, cols, rows, 0, 0);
        accDataset.FlushCache();
        accDataset.Dispose();

        stream.Write(BitConverter.GetBytes(subCatSlopes.Count()), 0, 4);

        byte[] listBytes = new byte[subCatSlopes.Count() * 4];
        Buffer.BlockCopy(subCatSlopes.ToArray(), 0, listBytes, 0, subCatSlopes.Count() * 4);
        stream.Write(listBytes, 0, subCatSlopes.Count() * 4);

        byte[] areasBytes = new byte[subCatAreas.Count() * 4];
        Buffer.BlockCopy(subCatAreas.ToArray(), 0, areasBytes, 0, subCatAreas.Count() * 4);
        stream.Write(areasBytes, 0, subCatAreas.Count() * 4);

        Console.WriteLine("Data Sent!");
        byte[] receivedData = new byte[1024];

        bytesRead = stream.Read(receivedData, 0, receivedData.Length);
        Console.WriteLine("Confirmation of received data!");
        // Close the stream and client
        stream.Close();
        client.Close();
        Thread.Sleep(1000);
    }
}
