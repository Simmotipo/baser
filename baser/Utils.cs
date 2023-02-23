using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace baser
{
    
    public class JsonUtils
    {
        public static string ToJson(string str, databaseManager db, char seperator = '|', char newline = '\n')
        {
            if (str.Split(seperator).Length == 1) return $"{{\"value\":\"{str}\"}}";
            string[] keys = db.getRow(0);
            string json = "[";
            bool doneTitles = false;
            foreach (string line in str.Split(newline))
            {
                if (line != "")
                {
                    json += "{";
                    for (int i = 0; i < keys.Length; i++)
                    {
                        json += $"\"{keys[i]}\":\"{line.Split(seperator)[i + 1]}\",";
                    }
                    json = json.Substring(0, json.Length - 1) + "},";
                }
            }
            json = json.Substring(0, json.Length - 1).Replace(Convert.ToString((char)Convert.ToByte(0)), "").Replace(" ", "") + "]";
            return json;

        }

        public static string ToRow(string json, databaseManager db, char seperator = '|')
        {
            return "";
        }
    }
}
