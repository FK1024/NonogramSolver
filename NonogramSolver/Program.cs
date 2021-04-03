using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Picross
{
    class Program
    {
        public const int Unknown = 0;
        public const int Blank = -1;
        public const int Set = 1;
        public const string UnknownStr = " ";
        public const string SetStr = "#";
        public const string BlankStr = " ";

        public static List<List<int>> rowSegments = new List<List<int>>();
        public static List<List<int>> colSegments = new List<List<int>>();
        // the game field (0 = unknown, -1 = blank, 1 = set)
        public static int[,] field;
        public static int fieldSize;

        public enum Dimension
        {
            Row,
            Column
        }

        static void Main(string[] args)
        {
            // the first and only argument must be the file containing the puzzle in the format '(r:|c:|(number )+)
            ParseFile(args[0]);

            fieldSize = rowSegments.Count;

            // check for overfilled rows/cols
            for (int i = 0; i < fieldSize; i++)
            {
                if (GetLaxity(rowSegments[i]) < 0)
                {
                    throw new InvalidDataException($"The segments of row {i + 1} are too long to fit in the game field");
                }
                if (GetLaxity(colSegments[i]) < 0)
                {
                    throw new InvalidDataException($"The segments of column {i + 1} are too long to fit in the game field");
                }
            }

            field = new int[fieldSize, fieldSize];

            //SetStartingGrid();
            Solve();

            PrintField(field);
        }

        // parses a given data file in which
        //   'r:' indicates, that the following lines of numbers are row segments and
        //   'c:' indicates, that the following lines of numbers are column segments
        // and sets the global variables 'rowSegments' and 'colSegments'
        public static void ParseFile(string file)
        {
            var lines = File.ReadAllLines(file);
            List<int> numbers;

            var parseMode = lines[0] switch
            {
                "r:" => Dimension.Row,
                "c:" => Dimension.Column,
                _ => throw new InvalidDataException($"the first line of data file has to begin either with 'r:' or with 'c:' but it begins with {lines[0]}"),
            };

            foreach (var line in lines.Skip(1))
            {
                switch (line)
                {
                    case "r:":
                        parseMode = Dimension.Row;
                        break;
                    case "c:":
                        parseMode = Dimension.Column;
                        break;
                    default:
                        numbers = line.Split(" ").ToList().ConvertAll(int.Parse);
                        if (parseMode == Dimension.Row)
                        {
                            rowSegments.Add(numbers);
                        }
                        else if (parseMode == Dimension.Column)
                        {
                            colSegments.Add(numbers);
                        }
                        break;
                }
            }

            var rowCount = rowSegments.Count;
            var colCount = colSegments.Count;
            if (rowCount != colCount) throw new InvalidDataException($"there has to be an equal amount of lines for row and column number but there were {rowCount} for rows and {colCount} for columns");
        }

        public static int GetLaxity(List<int> numbers)
        {
            return fieldSize - (numbers.Sum() + numbers.Count - 1);
        }

        public static void Solve()
        {
            var openRows = Enumerable.Range(0, fieldSize).ToList();
            var openCols = Enumerable.Range(0, fieldSize).ToList();
            List<int> openSet;

            var validRowConfigs = new List<int[]>[fieldSize];
            var validColConfigs = new List<int[]>[fieldSize];
            List<int[]>[] validConfigs;
            for (int i = 0; i < fieldSize; i++)
            {
                validRowConfigs[i] = GetConfigurations(rowSegments[i]);
                validColConfigs[i] = GetConfigurations(colSegments[i]);
            }

            bool solvedCell = true;
            var solvedItems = new List<int>();

            while (openRows.Any() && openCols.Any() && solvedCell)
            {
                solvedCell = false;
                foreach (var dim in (Dimension[])Enum.GetValues(typeof(Dimension)))
                {
                    solvedItems.Clear();

                    if (dim == Dimension.Row)
                    {
                        openSet = openRows;
                        validConfigs = validRowConfigs;
                    }
                    else
                    {
                        openSet = openCols;
                        validConfigs = validColConfigs;
                    }

                    foreach (var index in openSet)
                    {
                        validConfigs[index] = RemoveInvalidConfigs(dim, index, validConfigs[index]);
                        if (validConfigs[index].Count == 0)
                        {
                            throw new Exception("The puzzle was not solvable");
                        }
                        var transposed = TransposeConfig(validConfigs[index]);
                        for (int i = 0; i < transposed.Length; i++)
                        {
                            if (dim == Dimension.Row && field[index, i] == Unknown || dim == Dimension.Column && field[i, index] == Unknown)
                            {
                                List<int> cellStates = transposed[i];
                                // all valid configurations for this previously unknown cell have the same state => can be set in field
                                if (!cellStates.Any(v => v != cellStates[0]))
                                {
                                    solvedCell = true;
                                    if (dim == Dimension.Row)
                                    {
                                        field[index, i] = cellStates[0];
                                        if (ColSolved(i))
                                        {
                                            openCols.Remove(i);
                                        }
                                    }
                                    else
                                    {
                                        field[i, index] = cellStates[0];
                                        if (RowSolved(i))
                                        {
                                            openRows.Remove(i);
                                        }
                                    }
                                }
                            }
                        }

                        if (dim == Dimension.Row && RowSolved(index) || dim == Dimension.Column && ColSolved(index))
                        {
                            solvedItems.Add(index);
                        }
                    }

                    if (dim == Dimension.Row)
                    {
                        openRows = openRows.Except(solvedItems).ToList();
                    }
                    else
                    {
                        openCols = openCols.Except(solvedItems).ToList();
                    }
                }
            }

            if (!solvedCell)
            {
                throw new Exception("The puzzle was not solvable");
            }
        }

        private static bool RowSolved(int row)
        {
            for (int col = 0; col < fieldSize; col++)
            {
                if (field[row, col] == Unknown)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ColSolved(int col)
        {
            for (int row = 0; row < fieldSize; row++)
            {
                if (field[row, col] == Unknown)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<int[]> GetConfigurations(List<int> segments)
        {
            // a single 0 indicates a blank row/col and needs special treatment
            if (segments.Count == 1 && segments.First() == 0)
            {
                return new List<int[]> { Enumerable.Repeat(Blank, fieldSize).ToArray() };
            }

            // result list
            var configs = new List<int[]>();

            var laxity = GetLaxity(segments);

            var leftmostPositions = new List<int>(segments.Count);
            int pos = 0;

            foreach (var segment in segments)
            {
                leftmostPositions.Add(pos);
                pos += segment + 1;
            }

            // start with #segmentCount zeros
            var current = new int[segments.Count].ToList();
            var config = new List<int>(current.Zip(leftmostPositions, (shift, @base) => @base + shift));
            configs.Add(ToFieldSetup(segments, config));

            while (current.First() < laxity)
            {
                // find the last index having an increasable value
                int lastIncreasableIdx = current.Count - 1;
                while (current[lastIncreasableIdx] == laxity)
                {
                    lastIncreasableIdx--;
                }

                // increase that value
                current[lastIncreasableIdx]++;

                // set all following values to the current increased one
                for (int s = lastIncreasableIdx + 1; s < current.Count; s++)
                {
                    current[s] = current[lastIncreasableIdx];
                }

                // add current to result list
                config = new List<int>(current.Zip(leftmostPositions, (shift, @base) => @base + shift));
                configs.Add(ToFieldSetup(segments, config));
            }

            return configs;
        }

        private static int[] ToFieldSetup(List<int> segments, List<int> segmentStarts)
        {
            var setup = new int[fieldSize];

            for (int s = 0; s < segments.Count; s++)
            {
                int start;
                if (s == 0)
                {
                    start = 0;
                }
                else
                {
                    start = segmentStarts[s - 1] + segments[s - 1];
                }

                for (int blank = start; blank < segmentStarts[s]; blank++)
                {
                    setup[blank] = Blank;
                }

                for (int set = segmentStarts[s]; set < segmentStarts[s] + segments[s]; set++)
                {
                    setup[set] = Set;
                }
            }

            // fill up blanks after last segment
            for (int blank = segmentStarts[segmentStarts.Count - 1] + segments[segments.Count - 1]; blank < fieldSize; blank++)
            {
                setup[blank] = Blank;
            }

            return setup;
        }

        private static List<int[]> RemoveInvalidConfigs(Dimension dimension, int index, List<int[]> configurations)
        {
            return configurations.Where(c => IsValid(c, dimension, index)).ToList();
        }

        private static bool IsValid(int[] fieldSetup, Dimension dimension, int index)
        {
            if (dimension == Dimension.Row)
            {
                for (int c = 0; c < fieldSize; c++)
                {
                    if (!(field[index, c] == fieldSetup[c] || field[index, c] == Unknown))
                    {
                        return false;
                    }
                }
                return true;
            }
            else
            {
                for (int r = 0; r < fieldSize; r++)
                {
                    if (!(field[r, index] == fieldSetup[r] || field[r, index] == Unknown))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private static List<int>[] TransposeConfig(List<int[]> configs)
        {
            var transposed = new List<int>[configs.First().Length];

            // init
            for (int c = 0; c < transposed.Length; c++)
            {
                transposed[c] = new List<int>();
            }

            foreach (var config in configs)
            {
                for (int cellNo = 0; cellNo < config.Length; cellNo++)
                {
                    transposed[cellNo].Add(config[cellNo]);
                }
            }

            return transposed;
        }

        public static void SetStartingGrid()
        {
            SetSafePixels(rowSegments, Dimension.Row);
            SetSafePixels(colSegments, Dimension.Column);
        }

        // sets pixels in 'field' which are safe set because of the segment length and the row/col laxity
        // |       |<- laxity
        // |  ###  |<- safe set
        // |ooooo  |<- left most
        // |  ooooo|<- right most
        public static void SetSafePixels(List<List<int>> pixelVectors, Dimension dimension)
        {
            for (int d2 = 0; d2 < fieldSize; d2++)
            {
                List<int> numbers = pixelVectors[d2];
                var laxity = GetLaxity(numbers);
                var start = 0;

                for (int n = 0; n < numbers.Count; n++)
                {
                    for (int d1 = start + laxity; d1 < start + numbers[n]; d1++)
                    {
                        if (dimension == Dimension.Row)
                        {
                            field[d2, d1] = Set;
                        }
                        else if (dimension == Dimension.Column)
                        {
                            field[d1, d2] = Set;
                        }
                    }

                    start += numbers[n] + 1;
                }
            }
        }

        // prints the game field as grid with
        //   '#' for set pixels,
        //   'X' for unset pixels and
        //   ' ' for unknown cells
        public static void PrintField(int[,] field)
        {
            var rowSeparaterStr = new String('-', fieldSize);
            rowSeparaterStr = rowSeparaterStr.Replace("-", "+-") + "+\n";
            var str = rowSeparaterStr;

            for (int r = 0; r < fieldSize; r++)
            {
                str += "|";

                for (int c = 0; c < fieldSize; c++)
                {
                    switch (field[r, c])
                    {
                        case Set:
                            str += SetStr;
                            break;
                        case Blank:
                            str += BlankStr;
                            break;
                        case Unknown:
                            str += UnknownStr;
                            break;
                        default:
                            break;
                    }
                    str += "|";
                }
                str += "\n" + rowSeparaterStr;
            }

            Console.Write(str);
        }
    }
}
