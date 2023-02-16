using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace baser
{
    internal class databaseManager
    {
        string dbPath = "";
        ushort colCount;
        ushort colSize;
        byte[] cols;
        byte[] db;
        int bytesPerRow;
        bool changed = false;

        public databaseManager(string path)
        {
            if (!path.EndsWith(".dbr")) path += ".dbr";
            dbPath = path;
            byte[] dataIngress = File.ReadAllBytes(path);

            db = new byte[dataIngress.Length - 4];
            for (int i = 0; i < db.Length; i++) db[i] = dataIngress[i + 4];
            byte[] colBytes = { dataIngress[0], dataIngress[1] };
            colSize = BitConverter.ToUInt16(colBytes);
            colBytes =new byte[] { dataIngress[2], dataIngress[3] };
            colCount = BitConverter.ToUInt16(colBytes);

            Console.WriteLine($"Opened {path} with {colCount} cols of {colSize} bytes each.");
            bytesPerRow = colCount * colSize;
            bool running = true;
            while (running)
            {
                Console.Write("> ");
                string cmd = Console.ReadLine();
                switch (cmd.Split(' ')[0].ToLower())
                {
                    case "help":
                        Console.WriteLine("addrow {rows}\ngetrow {n}\nquery {query}\nsave\nclose");
                        break;
                    case "close":
                    case "exit":
                        if (!changed || cmd.Split(' ').Length > 1 && cmd.Split(' ')[1].ToLower() == "--nosave") running = false;
                        else Console.WriteLine("There are unsaved changes! Use exit --nosave to close without saving!");
                        break;
                    case "addrow":
                        bool status = addRow(cmd.Substring(7).Split(' '));
                        if (status) Console.WriteLine("Success");
                        else Console.WriteLine("Invalid");
                        break;
                    case "query":
                        query(cmd.Substring(6));
                        break;
                    case "save":
                        if (save()) Console.WriteLine("Success");
                        else Console.WriteLine("Failed");
                        break;
                    case "delrow":
                        if (delRow(Convert.ToInt32(cmd.Substring(7)))) Console.WriteLine("Success");
                        else Console.WriteLine("Error");
                        break;
                    case "editrow":
                        if (editRow(Convert.ToInt32(cmd.Split(' ')[1]), cmd.Split(' ').Skip(2).ToArray())) Console.WriteLine("Success");
                        else Console.WriteLine("Error");
                        break;
                    case "clear":
                    case "clr":
                    case "cls":
                        Console.Clear();
                        break;
                    case "print":
                    case "get":
                        Console.WriteLine();
                        print(Convert.ToInt32(cmd.Split(' ')[1]));
                        Console.WriteLine();
                        break;
                    case "dump":
                        int totalRows = db.Length / bytesPerRow;
                        for (int i = 0; i < totalRows; i++) print(i);
                        break;
                    default:
                        try
                        {
                            query(cmd);
                        }
                        catch (Exception e) { Console.WriteLine("Unknown Command"); }
                        break;
                }
            }

        }

        public int[] query(string equation)
        {
            try
            {
                //Options: .is .contains .starts and .ends
                //Example syntax: firstname.contains=John&mobile.starts=079&surname.contains~Doe
                //Searched for people with first name John, but not last name Doe, with mobile # starting 079

                string[] queries = equation.Split('&'); //Note: this is currently all implementing OR only.
                List<int> validRows = new List<int>(); //migrate away from List<>s
                foreach (string query in queries)
                {
                    foreach (int i in queryTask(query)) if (!validRows.Contains(i)) validRows.Add(i);
                }

                foreach (int i in validRows) print(i);
            }
            catch (Exception e) { Console.WriteLine("Invalid request"); }
            return null;
        }

        public void print(int row)
        {
            string output = "";
            foreach (string col in getRow(row)) output += $" | {col}";
            Console.WriteLine($"{row.ToString().PadLeft(4, ' ')} | {output.Substring(3).Replace($"{(char)Convert.ToByte(0)}", " ")}");
        }

        public int[] queryTask(string query)
        {
            try
            {
                List<int> validRows = new List<int>(); //migrate away from List<>s
                string colName = query.Split('.')[0];
                string colTerm = query.Split('.')[1].Split('=')[0].ToLower();
                string term = "";
                if (query.Contains("=")) term = query.Split('=')[1];
                else term = query.Split('~')[1];
                term = term.ToLower();
                
                ushort colNumber = 0;
                if (colName.Length > 1)
                {
                    //Get col No
                    string[] headings = getRow(0);
                    for (ushort i = 0; i < headings.Length; i++) if (headings[i].ToLower().Replace($"{(char)Convert.ToByte(0)}", "") == colName.ToLower()) colNumber = i;

                    int totalRows = db.Length / bytesPerRow;
                    for (int i = 0; i < totalRows; i++)
                    {
                        string reference = getRow(i)[colNumber].ToLower().Replace($"{(char)Convert.ToByte(0)}", "");
                        switch (colTerm)
                        {
                            case "is":
                                if (reference == term) validRows.Add(i);
                                break;
                            case "contains":
                                if (reference.Contains(term)) validRows.Add(i);
                                break;
                            case "starts":
                                if (reference.StartsWith(term)) validRows.Add(i);
                                break;
                            case "ends":
                                if (reference.EndsWith(term)) validRows.Add(i);
                                break;

                        }
                    }
                }
                else
                {

                    int totalRows = db.Length / bytesPerRow;
                    for (int i = 0; i < totalRows; i++)
                    {
                        for (colNumber = 0; colNumber < colCount; colNumber++)
                        {
                            string reference = getRow(i)[colNumber].ToLower().Replace($"{(char)Convert.ToByte(0)}", "");
                            switch (colTerm)
                            {
                                case "is":
                                    if (reference == term && !validRows.Contains(i)) validRows.Add(i);
                                    break;
                                case "contains":
                                    if (reference.Contains(term) && !validRows.Contains(i)) validRows.Add(i);
                                    break;
                                case "starts":
                                    if (reference.StartsWith(term) && !validRows.Contains(i)) validRows.Add(i);
                                    break;
                                case "ends":
                                    if (reference.EndsWith(term) && !validRows.Contains(i)) validRows.Add(i);
                                    break;

                            }
                        }
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
        }

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
                File.WriteAllBytes(dbPath, data.Concat(db).ToArray());
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
