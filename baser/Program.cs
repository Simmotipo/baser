﻿using System;

namespace baser
{
    class Controller
    {
        public static databaseManager dbMgr;
        public static string version = "1.5.7";
        public static void Main(string[] args)
        {
            string resp = "";
            if (args.Length > 0 && File.Exists(args[0]))
            {
                if (args.Length == 1) dbMgr = new databaseManager(args[0]);
                else dbMgr = new databaseManager(args[0], apiPort:Convert.ToUInt16(args[1]));
            }
            while (true)
            {
                try
                {
                    Console.Write("New {colSize} {colCount} {path}, Open {path}, Remote {http://URL:PORT}, or exit? ");
                    resp = Console.ReadLine();
                    switch (resp.ToUpper()[0])
                    {
                        case 'N':
                            createDB(resp.Split(' ')[3], Convert.ToUInt16(resp.Split(' ')[1]), Convert.ToUInt16(resp.Split(' ')[2]));
                            break;
                        case 'O':
                            dbMgr = new databaseManager(resp.Split(' ')[1]);
                            break;
                        case 'E':
                            Environment.Exit(0);
                            break;
                        case 'R':
                            dbMgr = new databaseManager(resp.Split(' ')[1], "remoteFile");
                            break;
                        case 'V':
                            Console.WriteLine(version);
                            break;
                    }
                }
                catch (Exception e) { Console.WriteLine(e); }
            }
        }

        public static bool createDB(string path, ushort colSize, ushort colCount)
        {
            try
            {
                if (!path.EndsWith(".dbr")) path += ".dbr";
                byte[] d1 = BitConverter.GetBytes(colSize);
                byte[] d2 = BitConverter.GetBytes(colCount);
                byte[] data = new byte[4];
                data[0] = d1[0];
                data[1] = d1[1];
                data[2] = d2[0];
                data[3] = d2[1];

                File.WriteAllBytes(path, data);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}