using HardHorn.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Archiving
{
    public class Parameter : IComparable
    {
        uint? _scale = null;
        public bool HasScale { get { return _scale.HasValue; } }
        public uint Scale
        {
            get { return _scale.Value; }
            set
            {
                if (HasScale)
                {
                    _scale = value;
                }
                else
                {
                    throw new InvalidOperationException("Parameter does not have a scale.");
                }
            }
        }

        uint? _precision = null;
        public bool HasPrecision { get { return _precision.HasValue; } }
        public uint Precision
        {
            get { return _precision.Value; }
            set
            {
                if (HasPrecision)
                {
                    _precision = value;
                }
                else
                {
                    throw new InvalidOperationException("Parameter does not have a precision.");
                }
            }
        }

        uint? _length = null;
        public bool HasLength { get { return _length.HasValue; } }
        public uint Length
        {
            get { return _length.Value; }
            set
            {
                if (HasLength)
                {
                    _length = value;
                }
                else
                {
                    throw new InvalidOperationException("Parameter does not have a length.");
                }
            }
        }

        private Parameter() { }

        public static Parameter WithPrecisionAndScale(uint precision, uint scale)
        {
            var param = new Parameter();
            param._precision = precision;
            param._scale = scale;
            return param;
        }

        public static Parameter WithLength(uint length)
        {
            var param = new Parameter();
            param._length = length;
            return param;
        }

        public static Parameter WithPrecision(uint precision)
        {
            var param = new Parameter();
            param._precision = precision;
            return param;
        }

        /// <summary>
        /// Parses a list of parameters in relation to a data type, to construct a parameter object.
        /// </summary>
        /// <param name="dataType">The data type.</param>
        /// <param name="parameters">The parameters to parse.</param>
        /// <returns>The parameter object.</returns>
        /// <exception cref="System.InvalidOperationException">When the parameters are invalid for the data type.</exception>
        public static Parameter Parse(DataType dataType, uint[] parameters)
        {
            // TODO: Handle default precision value (implementation defined)
            switch (dataType)
            {
                case DataType.CHARACTER:
                case DataType.CHARACTER_VARYING:
                case DataType.NATIONAL_CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                    if (parameters.Length == 1)
                    {
                        return WithLength(parameters[0]);
                    }
                    else if (parameters == null || parameters.Length == 0)
                    {
                        return WithLength(1);
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid parameters.");
                    }
                case DataType.NUMERIC:
                case DataType.DECIMAL:
                    if (parameters.Length == 2)
                    {
                        return WithPrecisionAndScale(parameters[0], parameters[1]);
                    }
                    else if (parameters.Length == 1)
                    {
                        return WithPrecisionAndScale(parameters[0], 0);
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid parameters.");
                    }
                case DataType.FLOAT:
                    if (parameters.Length == 1)
                    {
                        return WithPrecision(parameters[0]);
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid parameters.");
                    }
                case DataType.TIMESTAMP:
                case DataType.TIMESTAMP_WITH_TIME_ZONE:
                    if (parameters == null || parameters.Length == 0)
                    {
                        return WithPrecision(6);
                    }
                    else if (parameters.Length == 1)
                    {
                        return WithPrecision(parameters[0]);
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid parameters.");
                    }
                case DataType.TIME:
                case DataType.TIME_WITH_TIME_ZONE:
                    if (parameters == null || parameters.Length == 0)
                    {
                        return WithPrecision(0);
                    }
                    else if (parameters.Length == 1)
                    {
                        return WithPrecision(parameters[0]);
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid parameters");
                    }
                default:
                    return null;
            }
        }

        public override string ToString()
        {
            var parameters = new List<uint>();

            if (HasLength)
            {
                return "(" + Length + ")";
            } // TODO: Default CHAR parameter hidden?
            else if (HasPrecision && !HasScale)
            {
                return "(" + Precision + ")";
            }
            else if (HasPrecision && HasScale && Scale == 0)
            {
                return "(" + Precision + ")";
            }
            else if (HasPrecision && HasScale && Scale != 0)
            {
                return "(" + Precision + ", " + Scale + ")";
            }

            throw new InvalidOperationException("Invalid parameter");
        }

        public int CompareTo(object obj)
        {
            var otherParam = obj as Parameter;

            if (otherParam == null)
            {
                return 1;
            }
            else
            {
                return CompareTo(otherParam);
            }
        }

        public int CompareTo(Parameter other)
        {
            if (HasLength && other.HasLength)
            {
                return Length.CompareTo(other.Length);
            }
            else if (HasPrecision && other.HasPrecision && !HasScale && !other.HasScale)
            {
                return Precision.CompareTo(other.Precision);
            }
            else if (HasPrecision && other.HasPrecision && HasScale && other.HasScale)
            {
                var precisionComparison = Precision.CompareTo(other.Precision);

                if (precisionComparison == 0)
                {
                    return Scale.CompareTo(other.Scale);
                }
                else
                {
                    return precisionComparison;
                }
            }

            return 0;
        }
    }
}
