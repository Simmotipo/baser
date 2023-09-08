using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static System.Net.WebRequestMethods;

namespace baser
{
    public class databaseManager
    {
        string dbPath = "";
        ushort colCount;
        ushort colSize;
        byte[] cols;
        public byte[] db;
        public string[] localCmds = { "version", "ver", "info", "clear", "clr", "cls", "exit", "close"};
        int bytesPerRow;
        bool changed = false;
        bool localRunning = true;
        bool holdMainThread = true;
        bool apiRunning = false;
        apiManager api = null;

        public databaseManager(string path, string mode = "localFile", ushort apiPort = 0, bool haveLocalRunning = true)
        {
            localRunning = haveLocalRunning;
            dbPath = path;
            byte[] dataIngress = null;
            if (mode == "localFile") 
            {
                if (!path.EndsWith(".dbr")) path += ".dbr";

                dataIngress = System.IO.File.ReadAllBytes(path);
                db = new byte[dataIngress.Length - 4];
                for (int i = 0; i < db.Length; i++) db[i] = dataIngress[i + 4];
                byte[] colBytes = { dataIngress[0], dataIngress[1] };
                colSize = BitConverter.ToUInt16(colBytes);
                colBytes = new byte[] { dataIngress[2], dataIngress[3] };
                colCount = BitConverter.ToUInt16(colBytes);

                Console.WriteLine($"Opened {path} with {colCount} cols of {colSize} bytes each.");
                bytesPerRow = colCount * colSize;
            }
            else
            {
                if (!dbPath.StartsWith("http://")) dbPath = "http://" + dbPath;
                if (dbPath.EndsWith("/")) dbPath = dbPath.Substring(0, dbPath.Length - 1);
            }

            if (apiPort != 0) Do($"enableapi {apiPort}", "localFile");

            while (localRunning)
            {
                try
                {
                    Console.Write("> ");
                    string cmd = Console.ReadLine();
                    if (cmd.Replace(" ", "") != "")
                    {
                        if (mode == "localFile" || localCmds.Contains(cmd.Split(' ')[0])) Console.WriteLine(Do(cmd, mode));
                        else
                        {
                            if (cmd.Split(' ')[0].ToLower() == "disableapi") Console.WriteLine("ERR: You cannot run disableapi from remote client!");
                            else
                            {
                                using (var httpClient = new HttpClient())
                                {
                                    using (var request = new HttpRequestMessage(new HttpMethod("GET"), $"{dbPath}/{cmd}"))
                                    {
                                        var response = httpClient.SendAsync(request);
                                        Console.WriteLine(response.Result.Content.ReadAsStringAsync().Result);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e) { }
            }
            while (holdMainThread) { System.Threading.Thread.Sleep(60000); }
        }

        public string Do(string cmd, string mode, string source = "localFile")
        {
            if (cmd.Replace(" ", "") == "") return "";
            switch (cmd.Split(' ')[0].ToLower())
            {
                case "help":
                    return "addrow {cols}\nclear\nclose\ndelrow {n}\ndisableapi\ndump\neditrow {n} {cols}\nenableapi {port} (requires Admin/Sudo)\ngetrow {n}\nquery {query}\nsave\nsum {columnNo} {query}\nversion";
                case "enableapi":
                case "openapi":
                    if (api != null) return "The API is already started";
                    else
                    {
                        try
                        {
                            api = new apiManager(Convert.ToUInt16(cmd.Split(' ')[1]), this);
                            return "Started";
                        }
                        catch (Exception e)
                        {
                            return "Error occured. Are you definitely running in Admin/Sudo?";
                        }
                    }
                case "disableapi":
                    if (api == null) return "API is not currently running";
                    else
                    {
                        api.runServer = false;
                        while (!api.deleteable) Thread.Sleep(10);
                        api = null;
                        return "API Stopped";
                    }
                case "getfilebytes":
                    if (!dbPath.EndsWith(".dbr")) return ASCIIEncoding.ASCII.GetString(System.IO.File.ReadAllBytes(dbPath += ".dbr"));
                    else return ASCIIEncoding.ASCII.GetString(System.IO.File.ReadAllBytes(dbPath)); //This should just return the ASCII equivalent of the DB. Receiver can then decode
                case "close":
                case "exit":
                    if (!changed || cmd.Split(' ').Length > 1 && cmd.Split(' ')[1].ToLower() == "--nosave" && source == "local") { localRunning = false; holdMainThread = false; }
                    else return "There are unsaved changes! Use exit --nosave to close without saving!";
                    return "";
                case "addrow":
                    bool status = addRow(cmd.Substring(7).Split(' '));
                    if (status) return "Success";
                    else return "Invalid";
                case "query":
                    return query(cmd.Substring(6));
                case "sum":
                    try
                    {
                        cmd = cmd.Substring(4);
                        ushort c = Convert.ToUInt16(cmd.Split(' ')[0]);
                        if (cmd.Substring(cmd.Split(' ')[0].Length).Length < 2) return "Invalid SUM syntax";
                        string rows = query(cmd.Substring(cmd.Split(' ')[0].Length));
                        decimal t = 0;
                        if (c > rows.Split('\n')[0].Split('|').Length || Convert.ToString(c) != cmd.Split(' ')[0]) return "Invalid SUM syntax.";
                        foreach (string row in rows.Split('\n')) try { t += Convert.ToDecimal(row.Split('|')[c + 1]); } catch (Exception e) { }
                        return Convert.ToString(t); 
                    }
                    catch (Exception e)
                    {
                        return "Invalid SUM syntax.";
                    }
                case "save":
                    if (save()) return "Success";
                    else return "Failed";
                case "delrow":
                    if (delRow(Convert.ToInt32(cmd.Substring(7)))) return "Success";
                    else return "Error";
                case "editrow":
                    if (editRow(Convert.ToInt32(cmd.Split(' ')[1]), cmd.Split(' ').Skip(2).ToArray())) return "Success";
                    else return "Error";
                case "clear":
                case "clr":
                case "cls":
                    Console.Clear();
                    return "";
                case "print":
                case "get":
                case "getrow":
                    return "\n" + print(Convert.ToInt32(cmd.Split(' ')[1])) + "\n";
                case "dump":
                    string output = "";
                    int totalRows = db.Length / bytesPerRow;
                    for (int i = 0; i < totalRows; i++) output += print(i);
                    return output;
                case "version":
                case "ver":
                case "info":
                    if (mode == "localFile") return Controller.version;
                    else
                    {
                        using (var httpClient = new HttpClient())
                        {
                            using (var request = new HttpRequestMessage(new HttpMethod("GET"), $"{dbPath}/version"))
                            {
                                var response = httpClient.SendAsync(request);
                                return $"Local {Controller.version} | Remote {response.Result.Content.ReadAsStringAsync().Result}";
                            }
                        }
                    }
                default:
                    try
                    {
                        return query(cmd);
                    }
                    catch (Exception e) { return "Unknown Command"; }
            }
        }

        public byte[] pullFileContents()
        {
            return System.IO.File.ReadAllBytes(dbPath);
        }

        public string print(int row)
        {
            string output = "";
            foreach (string col in getRow(row)) output += $" | {col}";
            return $"{row.ToString().PadLeft(4, ' ')} | {output.Substring(3).Replace($"{(char)Convert.ToByte(0)}", " ")}\n";
        }

        public string query(string equation)
        {
            try
            {
                //NOTE: Operation order is important. E.G.:
                // A, B, C, D
                // 1, 2, 3, 4
                // X, Y, Z, 4
                // M, N, O, P

                //d.is=4&a.is=1%a.is=M
                //Will return "1, 2, 3, 4" and "M, N, O, P"
                //This is because splits occur at %. Hence
                //d.is=4&a.is=1%a.is=M is seen as
                //(d.is=4 AND a.is=1) OR a.is=M

                List<int> validRows = new List<int>();

                validRows.Add(0);

                string[] queries = equation.Split('%'); //Note: this is currently all implementing OR only.
                foreach (string query in queries)
                {
                    foreach (int i in runQuery(query.Split('&'))) if (!validRows.Contains(i)) validRows.Add(i);
                }


                string output = "";
                validRows.Sort();
                foreach (int i in validRows) output += print(i);
                return output;
            }
            catch (Exception e) { return "Invalid request"; }
        }

        public int[] runQuery(string[] queries)
        {
            int[] validRows = null;
            for (int e = 0; e < queries.Length; e++)
            {
                if (e == 0) validRows = queryTask(queries[e]);
                else
                {
                    validRows = queryTask(queries[e], validRows);
                }
            }
            return validRows.ToArray();
        }

        public int[] queryTask(string query, int[] rowIndex = null)
        {
            try
            {
                List<int> validRows = new List<int>(); //migrate away from List<>s
                string colName = query.Split('.')[0];
                bool boolean = true;
                string colTerm = query.Split('.')[1].Split('=')[0].ToLower();

                if (query.Contains('~')) { colTerm = query.Split('.')[1].Split('~')[0].ToLower(); boolean = false; }
                string term = "";
                if (query.Contains("=")) term = query.Split('=')[1];
                else term = query.Split('~')[1];
                term = term.ToLower();

                //Get col No
                ushort colNumber = 0;
                string[] headings = getRow(0);
                for (ushort i = 0; i < headings.Length; i++) if (headings[i].ToLower().Replace($"{(char)Convert.ToByte(0)}", "") == colName.ToLower()) colNumber = i;

                int totalRows = db.Length / bytesPerRow;

                if (rowIndex == null)
                {
                    List<int> t = new List<int>();
                    for (int x = 0; x < totalRows; x++) t.Add(x);
                    rowIndex = t.ToArray();
                }

                if (colName.Length > 1)
                {
                    for (int i = 0; i < rowIndex.Length; i++)
                    {
                        string reference = getRow(rowIndex[i])[colNumber].ToLower().Replace($"{(char)Convert.ToByte(0)}", "");
                        switch (colTerm)
                        {
                            case "is":
                                if ((reference == term).Equals(boolean)) validRows.Add(rowIndex[i]);
                                break;
                            case "contains":
                            case "has":
                                if (reference.Contains(term).Equals(boolean)) validRows.Add(rowIndex[i]);
                                break;
                            case "begins":
                            case "starts":
                                if (reference.StartsWith(term).Equals(boolean)) validRows.Add(rowIndex[i]);
                                break;
                            case "ends":
                                if (reference.EndsWith(term).Equals(boolean)) validRows.Add(rowIndex[i]);
                                break;

                        }
                    }
                }
                else
                {
                    for (int i = 0; i < rowIndex.Length; i++)
                    {
                        List<int> validRowsQueue = new List<int>();
                        bool breakMe = false;
                        for (colNumber = 0; colNumber < colCount; colNumber++)
                        {
                            string reference = getRow(rowIndex[i])[colNumber].ToLower().Replace($"{(char)Convert.ToByte(0)}", "");
                            switch (colTerm)
                            {
                                case "is":
                                    if (reference == term)
                                    {
                                        if (boolean) { if (!validRows.Contains(rowIndex[i])) { validRows.Add(rowIndex[i]); } breakMe = true; }
                                        else breakMe = true;
                                    }
                                    else
                                    {
                                        if (!boolean) { validRowsQueue.Add(rowIndex[i]);}
                                    }
                                    break;
                                case "contains":
                                case "has":
                                    if (reference.Contains(term))
                                    {
                                        if (boolean) { if (!validRows.Contains(rowIndex[i])) { validRows.Add(rowIndex[i]); } breakMe = true; }
                                        else breakMe = true;
                                    }
                                    else
                                    {
                                        if (!boolean) { validRowsQueue.Add(rowIndex[i]);}
                                    }
                                    break;
                                case "begins":
                                case "starts":
                                    if (reference.StartsWith(term))
                                    {
                                        if (boolean) { if (!validRows.Contains(rowIndex[i])) { validRows.Add(rowIndex[i]); } breakMe = true; }
                                        else breakMe = true;
                                    }
                                    else
                                    {
                                        if (!boolean) { validRowsQueue.Add(rowIndex[i]);}
                                    }
                                    break;
                                case "ends":
                                    if (reference.EndsWith(term))
                                    {
                                        if (boolean) { if (!validRows.Contains(rowIndex[i])) { validRows.Add(rowIndex[i]); } breakMe = true; }
                                        else breakMe = true;
                                    }
                                    else
                                    {
                                        if (!boolean) { validRowsQueue.Add(rowIndex[i]);}
                                    }
                                    break;

                            }
                            if (breakMe) break;
                        }
                        if (!boolean && !breakMe) foreach (int rowInt in validRowsQueue) if (!validRows.Contains(rowInt)) validRows.Add(rowInt);
                    }
                }

                return validRows.ToArray();
            }
            catch (Exception e)
            {
                return null;
            }

        }

        public bool delRow(int rowNum)
        {
            try
            {
                int startNum = rowNum * colSize * colCount;
                byte[] a = db.Take(startNum).ToArray();
                byte[] b = db.TakeLast(db.Length - (startNum + (colSize * colCount))).ToArray();
                db = a.Concat(b).ToArray();
                changed = true;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool editRow(int rowNum, string[] cols)
        {


            byte[] newData = new byte[colCount * colSize];
            if (cols.Length > colCount) return false;
            ASCIIEncoding ascii = new ASCIIEncoding();
            for (int i = 0; i < colCount; i++)
            {
                string col = cols[i].PadRight(colSize, (char)Convert.ToByte(0));
                if (col.Length > colSize) return false;
                byte[] newColData = new byte[colSize];
                newColData = ascii.GetBytes(col);
                for (int x = 0; x < colSize; x++) newData[(i * colSize) + x] = newColData[x];
            }

            int startNum = rowNum * colSize * colCount;
            byte[] a = db.Take(startNum).ToArray();
            byte[] b = db.TakeLast(db.Length - (startNum + (colSize * colCount))).ToArray();
            db = a.Concat(newData).Concat(b).ToArray();
            changed = true;
            return true;
        }

        public string[] getRow(int rowNum)
        {
            int startNum = rowNum * (colSize * colCount);
            string[] row = new string[colCount];
            for (ushort i = 0; i < colCount; i++)
            {
                byte[] bytes = new byte[colSize];
                for (ushort x = 0; x < colSize; x++) bytes[x] = db[startNum + (i * colSize) + x];
                row[i] = new ASCIIEncoding().GetString(bytes, 0, colSize);
            }
            return row;
        }
        public bool addRow(string[] cols)
        {
            try
            {
                byte[] newData = new byte[colCount * colSize];
                if (cols.Length > colCount) return false;
                ASCIIEncoding ascii = new ASCIIEncoding();
                for (int i = 0; i < colCount; i++)
                {
                    string col = cols[i].PadRight(colSize, (char)Convert.ToByte(0));
                    if (col.Length > colSize) return false;
                    byte[] newColData = new byte[colSize];
                    newColData = ascii.GetBytes(col);
                    for (int x = 0; x < colSize; x++) newData[(i * colSize) + x] = newColData[x];
                }
                db = db.Concat(newData).ToArray();
                changed = true;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool sortByColumn(ushort colNumber)
        {
            return false;
        } //Not yet implemented

        public bool save()
        {
            try
            {
                byte[] d1 = BitConverter.GetBytes(colSize);
                byte[] d2 = BitConverter.GetBytes(colCount);
                byte[] data = new byte[4];
                data[0] = d1[0];
                data[1] = d1[1];
                data[2] = d2[0];
                data[3] = d2[1];
                System.IO.File.WriteAllBytes(dbPath, data.Concat(db).ToArray());
                changed = false;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }
        
    }
}
