using System;
using System.Collections.Generic;

namespace TimeToBuild
{
    public static class FormulaParser
    {
        public static double ParseAndComputeFormula(string formula, params Dictionary<string, double>[] variables)
        {
            if (formula.Length == 0) return 0;

            int currentPosition = 0;
            double result = ParseExpression();

            if (currentPosition < formula.Length) throw new Exception($"Unexpected character: {formula[currentPosition]}");

            return result;

            char Current()
            {
                return currentPosition < formula.Length ? formula[currentPosition] : '\0';
            }

            bool Match(char c)
            {
                if (Current() == c)
                {
                    currentPosition++;
                    return true;
                }

                return false;
            }

            double ParseExpression()
            {
                double value = ParseTerm();
                while (true)
                {
                    if (Match('+')) value += ParseTerm();
                    else if (Match('-')) value -= ParseTerm();
                    else break;
                }
                return value;
            }

            double ParseTerm()
            {
                double value = ParsePower();
                while (true)
                {
                    if (Match('*')) value *= ParsePower();
                    else if (Match('/')) value /= ParsePower();
                    else break;
                }
                return value;
            }

            double ParsePower()
            {
                double value = ParseFactor();
                if (Match('^'))
                {
                    double exponent = ParsePower();
                    value = (double)Math.Pow(value, exponent);
                }
                return value;
            }

            double ParseFactor()
            {
                if (Match('+')) return ParseFactor();
                if (Match('-')) return -ParseFactor();
                if (Match('('))
                {
                    double value = ParseExpression();
                    if (!Match(')')) throw new Exception(") expected");
                    return value;
                }

                if (char.IsLetter(Current()))
                {
                    int startPosition = currentPosition;
                    while (char.IsLetterOrDigit(Current()) || Current() == '_') currentPosition++;

                    string variableName = formula.Substring(startPosition, currentPosition - startPosition);

                    double value = 0;
                    foreach (var vars in variables)
                    {
                        if (vars.TryGetValue(variableName, out value)) return value;
                    }

                    throw new Exception($"Unknown variable: {variableName}");
                }

                return ParseNumber();
            }

            double ParseNumber()
            {
                int startPosition = currentPosition;

                while (char.IsDigit(Current()) || Current() == '.') currentPosition++;

                return double.Parse(formula.Substring(startPosition, currentPosition - startPosition));
            }
        }
    }
}