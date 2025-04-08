using System.Data.Common;
using System.Reflection;

namespace transport.infraestructure.Database.Helpers;
public static class DbDataReaderExtensions
{
    public static List<T> MapToList<T>(this DbDataReader dr)
    {
        var objList = new List<T>();

        if (dr.HasRows)
        {
            var tType = typeof(T);
            bool isSingleValue = tType.IsPrimitive || tType == typeof(string) || tType == typeof(int?) || tType == typeof(decimal?);
            IEnumerable<PropertyInfo> props = null;
            Dictionary<string, DbColumn> colMapping = null;

            if (!isSingleValue)
            {
                props = typeof(T).GetRuntimeProperties();
                colMapping = dr.GetColumnSchema()
                    .Where(x => props.Any(y => y.Name.ToLower() == x.ColumnName.ToLower()))
                    .ToDictionary(key => key.ColumnName.ToLower());
            }

            while (dr.Read())
            {
                T obj;
                if (isSingleValue)
                {
                    if (!dr.IsDBNull(0))
                    {
                        obj = (T)dr.GetValue(0);
                    }
                    else
                    {
                        obj = default(T);
                    }
                }
                else
                {
                    obj = Activator.CreateInstance<T>();
                    foreach (var prop in props)
                    {
                        string propertyName = prop.Name.ToLower();
                        if (!colMapping.ContainsKey(propertyName))
                        {
                            continue;
                        }

                        var val = dr.GetValue(colMapping[propertyName].ColumnOrdinal.Value);
                        if (val != DBNull.Value)
                        {
                            // enum property
                            if (prop.PropertyType.IsEnum)
                            {
                                prop.SetValue(obj, Enum.ToObject(prop.PropertyType, val));
                            }
                            // nullable enum property
                            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && Nullable.GetUnderlyingType(prop.PropertyType).IsEnum)
                            {
                                prop.SetValue(obj, Enum.ToObject(Nullable.GetUnderlyingType(prop.PropertyType), val));
                            }
                            else
                            {
                                prop.SetValue(obj, val);
                            }
                        }
                    }
                }

                objList.Add(obj);
            }
        }

        return objList;
    }

    public static T MapToObject<T>(this DbDataReader dr)
    {
        var props = typeof(T).GetRuntimeProperties();

        if (dr.HasRows)
        {
            var colMapping = dr.GetColumnSchema()
                .Where(x => props.Any(y => y.Name.ToLower() == x.ColumnName.ToLower()))
                .ToDictionary(key => key.ColumnName.ToLower());

            if (dr.Read())
            {
                T obj = Activator.CreateInstance<T>();
                foreach (var prop in props)
                {
                    var val = dr.GetValue(colMapping[prop.Name.ToLower()].ColumnOrdinal.Value);
                    prop.SetValue(obj, val == DBNull.Value ? null : val);
                }

                return obj;
            }
        }

        return default(T);
    }

}
