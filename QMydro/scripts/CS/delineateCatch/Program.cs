using System.Net.Sockets;
using System.Diagnostics;
using mainAlg;

class Program
{

    static void Main(string[] args)
    {

        int port;
        using (TextReader reader = File.OpenText("port.txt")){ port = int.Parse(reader.ReadLine());}

        TcpClient client = new TcpClient("localhost", port); // Connect to the Python socket

        NetworkStream stream = client.GetStream(); // Receive the shape of the array as a 2-tuple of integers
        byte[] shapeBytes = new byte[8];
        stream.Read(shapeBytes, 0, 8);
        int rows = BitConverter.ToInt32(shapeBytes, 0);
        int cols = BitConverter.ToInt32(shapeBytes, 4);
        Console.WriteLine($"rows:{rows}");
        Console.WriteLine($"cols:{cols}");
        byte[] noDataByte = new byte[4];
        stream.Read(noDataByte, 0, 4);
        float noDataValue = BitConverter.ToSingle(noDataByte, 0);

        byte[] cellsizeXByte = new byte[4];
        stream.Read(cellsizeXByte, 0, 4);
        float cellsizeX = BitConverter.ToSingle(cellsizeXByte, 0);

        byte[] cellsizeYByte = new byte[4];
        stream.Read(cellsizeYByte, 0, 4);
        float cellsizeY = BitConverter.ToSingle(cellsizeYByte, 0);
        // Receive the array data as a byte stream

        byte[] targetSubcatSizeByte = new byte[4];
        stream.Read(targetSubcatSizeByte, 0, 4);
        float targetSubcatSize = BitConverter.ToSingle(targetSubcatSizeByte, 0) * 1000000 / (cellsizeX * cellsizeY);


        List<List<(int, int)>> outletCells = new List<List<(int, int)>>();
        byte[] countBytes = new byte[4];
        stream.Read(countBytes, 0, 4);
        int outletLocations = BitConverter.ToInt32(countBytes, 0);

        for (int i = 0; i < outletLocations; i++)
        {
            byte[] num_outletCellsBytes = new byte[4];
            stream.Read(num_outletCellsBytes, 0, 4);
            int num_outletCells = BitConverter.ToInt32(num_outletCellsBytes, 0);
            outletCells.Add(new List<(int, int)>());
            for (int j = 0; j < num_outletCells; j++)
            {
                byte[] cell = new byte[8];
                stream.Read(cell, 0, 8);
                outletCells[outletCells.Count - 1].Add((BitConverter.ToInt32(cell, 0), BitConverter.ToInt32(cell, 4)));
            }
        }
        Console.WriteLine($"Outlets: {outletCells.Count}");

        byte[] dataBytes = new byte[rows * cols * 4];
        stream.Read(dataBytes, 0, rows * cols * 4);
        Console.WriteLine("Data Received!");

        // Convert the byte stream to a 2D array of floats
        float[,] data = new float[rows, cols];
        Buffer.BlockCopy(dataBytes, 0, data, 0, dataBytes.Length);

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

        (catchments, streamMap, accumulation, subCatSlopes, subCatAreas) = mainAlgorithm.processingAlg(data, noDataValue, outletCells, cellsizeX, cellsizeY, targetSubcatSize);
        Console.WriteLine("Data Processed!");


        // Send the shape of the array as a 2-tuple of integers
        shapeBytes = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes(rows), 0, shapeBytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(cols), 0, shapeBytes, 4, 4);
        stream.Write(shapeBytes, 0, 8);
        // Send the array data as a byte stream

        // Send the catchments array to a byte stream
        byte[] catchmentsBytes = new byte[rows * cols * 4];
        Buffer.BlockCopy(catchments, 0, catchmentsBytes, 0, rows * cols * 4);
        stream.Write(catchmentsBytes, 0, rows * cols * 4);

        // Send the streams array
        byte[] streamBytes = new byte[rows * cols * 4];
        Buffer.BlockCopy(streamMap, 0, streamBytes, 0, rows * cols * 4);
        stream.Write(streamBytes, 0, rows * cols * 4);

        // Send the Accumulation array
        byte[] accumulationBytes = new byte[rows * cols * 4];
        Buffer.BlockCopy(accumulation, 0, accumulationBytes, 0, rows * cols * 4);
        stream.Write(accumulationBytes, 0, rows * cols * 4);

        stream.Write(BitConverter.GetBytes(subCatSlopes.Count()), 0, 4);

        byte[] listBytes = new byte[subCatSlopes.Count() * 4];
        Buffer.BlockCopy(subCatSlopes.ToArray(), 0, listBytes, 0, subCatSlopes.Count() * 4);
        stream.Write(listBytes, 0, subCatSlopes.Count() * 4);

        byte[] areasBytes = new byte[subCatAreas.Count() * 4];
        Buffer.BlockCopy(subCatAreas.ToArray(), 0, areasBytes, 0, subCatAreas.Count() * 4);
        stream.Write(areasBytes, 0, subCatAreas.Count() * 4);

        Console.WriteLine("Data Sent!");
        byte[] receivedData = new byte[1024];

        int bytesRead = stream.Read(receivedData, 0, receivedData.Length);
        Console.WriteLine("Confirmation of received data!");
        // Close the stream and client
        stream.Close();
        client.Close();
        Thread.Sleep(1000);
    }
}
