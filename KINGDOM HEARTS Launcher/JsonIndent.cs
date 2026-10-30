using System.Text;

namespace Kingdom_Hearts_Launcher
{
    // JavaScriptSerializer writes everything on one line; this makes launcher_config.json readable for hand-editing.
    public static class JsonIndent
    {
        public static string Format(string json)
        {
            StringBuilder sb = new StringBuilder();
            int depth = 0;
            bool inString = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < json.Length)
                    {
                        sb.Append(json[++i]);
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        sb.Append(c);
                        break;
                    case '{':
                    case '[':
                        sb.Append(c);
                        if (i + 1 < json.Length && (json[i + 1] == '}' || json[i + 1] == ']'))
                        {
                            sb.Append(json[++i]); // empty object or array stays on one line
                        }
                        else
                        {
                            depth++;
                            NewLine(sb, depth);
                        }
                        break;
                    case '}':
                    case ']':
                        depth--;
                        NewLine(sb, depth);
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        NewLine(sb, depth);
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static void NewLine(StringBuilder sb, int depth)
        {
            sb.Append('\n').Append(' ', depth * 2);
        }
    }
}
