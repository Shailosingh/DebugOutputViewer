using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Text;
using System.IO;

namespace DebugOutputViewer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Constants
            string PROCESS_NAME = "Folderify";
            int BUFFER_SIZE = 4096;
            byte[] EMPTY_BUFFER = new byte[BUFFER_SIZE];

            //Wait for process to start
            Process[] processListFound;
            do
            {
                processListFound = Process.GetProcessesByName(PROCESS_NAME);
                Thread.Sleep(1000);
            } 
            while (processListFound.Length == 0);

            //Record the monitored process object
            Process monitoredProcess = processListFound[0];

            //Print out stats
            Console.WriteLine($"Process Name: {monitoredProcess.ProcessName}");
            Console.WriteLine($"Process ID: {monitoredProcess.Id}");
            Console.WriteLine($"Process Start Time: {monitoredProcess.StartTime}");
            Console.WriteLine($"Main Window Title: {monitoredProcess.MainWindowTitle}");
            Console.WriteLine($"Responding: {monitoredProcess.Responding}");
            Console.WriteLine($"Physical Memory Usage: {monitoredProcess.WorkingSet64} bytes\n");

            //Create events
            EventWaitHandle DBWIN_BUFFER_READY = new EventWaitHandle(true, EventResetMode.AutoReset, "DBWIN_BUFFER_READY");
            EventWaitHandle DBWIN_DATA_READY = new EventWaitHandle(false, EventResetMode.AutoReset, "DBWIN_DATA_READY");

            //Create shared memory-segment
            MemoryMappedFile DBWIN_BUFFER = MemoryMappedFile.CreateOrOpen("DBWIN_BUFFER", BUFFER_SIZE, MemoryMappedFileAccess.ReadWrite);

            //Create loop that will print every new debug output and its process ID
            while (true)
            {
                //Let applications know that the buffer is ready for debug input
                DBWIN_BUFFER_READY.Set();

                //Wait for the application to write to the buffer
                DBWIN_DATA_READY.WaitOne();

                //Open the shared memory segment
                using (MemoryMappedViewStream stream = DBWIN_BUFFER.CreateViewStream())
                {
                    //Create a reader for the stream
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        //Read the process ID and if it is not the monitored process ID, discard it
                        int processID = reader.ReadInt32();
                        if(processID != monitoredProcess.Id)
                        {
                            continue;
                        }

                        //Read the debug output into byte buffer
                        byte[] debugStringBuffer = reader.ReadBytes(BUFFER_SIZE - sizeof(UInt32));
                        debugStringBuffer[debugStringBuffer.Length - 1] = 0;

                        //Convert to ASCII and print debug string to console
                        string debugOutput = Encoding.ASCII.GetString(debugStringBuffer);
                        Console.Write(debugOutput);
                    }
                }

                //Clear entire memory segment
                using (MemoryMappedViewStream stream = DBWIN_BUFFER.CreateViewStream())
                {
                    stream.Write(EMPTY_BUFFER, 0, BUFFER_SIZE);
                }
            }
        }
    }
}