using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace DataObjectExtension
{

  /// <summary>
  /// The 'Map' struct holds the mapping information which is 
  /// used during a comparison or copy operation needed to map 
  /// two disparate object properties on each other.
  /// </summary>
  public struct Map
  {
    /// <summary>
    /// The name of the source property.
    /// </summary>
    /// <value></value>
    public string SourceName
    {
      get;
      private set;
    }

    /// <summary>
    /// The name of the target property.
    /// </summary>
    /// <value></value>
    public string TargetName
    {
      get;
      private set;
    }

    /// <summary>
    /// This flag tells if the struct should be considered empty.
    /// </summary>
    /// <value></value>
    public bool IsEmpty
    {
      get
      {
        return ((string.IsNullOrWhiteSpace(SourceName)) || (string.IsNullOrWhiteSpace(TargetName)));
      }
    }

    /// <summary>
    /// This delegate is used to convert the value
    /// of the source property into the type which
    /// is required by the target property.
    /// </summary>
    public Func<object, object> Convert;

    /// <summary>
    /// Constructor of the 'Map' struct.
    /// </summary>
    /// <param name="sourceName"></param>
    /// <param name="targetName"></param>
    /// <param name="convert"></param>
    /// <exception cref="System.ArgumentException"></exception>
    public Map(string sourceName, string targetName, Func<object, object> convert = null)
    {
      string className;
      string methodName;

      if (!String.IsNullOrWhiteSpace(sourceName) && String.IsNullOrWhiteSpace(targetName) ||
         String.IsNullOrWhiteSpace(sourceName) && !String.IsNullOrWhiteSpace(targetName))
      {
        className = MethodBase.GetCurrentMethod().DeclaringType.FullName;
        methodName = MethodBase.GetCurrentMethod().Name;
        throw new System.ArgumentException($"One of the Arguments '{nameof(sourceName)}, {nameof(targetName)}' is null, empty or white space while the other isn't, what is not allowed. Either both arguments must be null or both arguments must be valid identifiers in function {className + "." + methodName}", nameof(sourceName) + ", " + nameof(targetName));
      }

      this.SourceName = sourceName;
      this.TargetName = targetName;

      if (convert == null)
      {
        //
        // NOP delegate. 
        //
        this.Convert = (sourceValue) => { return sourceValue; };
      }
      else
      {
        this.Convert = convert;
      }
    }
  }//END struct

  /// <summary>
  /// The DataObjectExtension class adds extension functions to 
  /// all objects. Those functions are supposed to be used on data
  /// objects. The extension functions support change detection,
  /// a function to compare data objects and a function to copy
  /// data values from one data object to another data object.
  /// </summary>
  public static class DataObjectExtension
  {

    /// <summary>
    /// The function creates a hash value over all public readable value 
    /// properties and string properties of the current object.
    /// The hash value can be used to detect changes in the 
    /// object. Calculate the hash value of an object before
    /// an operation and afterwards. If the values are equal,
    /// the object didn't change during the operation.
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="TSelf"></typeparam>
    /// <param name="excludeSelect"></param>
    /// <returns>byte[], the hash value</returns>
    public static byte[] CreateHash<TSelf>(this TSelf self, Func<PropertyInfo, bool> excludeSelect = null)
    {
      List<PropertyInfo> propertyInfoList;
      string selfHashString;

      propertyInfoList = self.GetType().GetProperties()
      .Where(item => item.CanRead)
      .ToList();

      if(excludeSelect == null)
      {
        //
        // NOP exclude selection. 
        //
        excludeSelect = (selfObj) => { return false;};
      }

      selfHashString = string.Empty;
      foreach (var property in propertyInfoList)
      {
        if(excludeSelect(property))
        {
          //
          // Current property is exclude. Continue with the next.
          //
          continue;
        }

        if (property.PropertyType.IsPrimitive || property.PropertyType.IsValueType || property.PropertyType == typeof(string))
        {
          //
          // Create the hash string from the current
          // property value.
          //
          selfHashString += (property.GetValue(self)?.GetHashCode().ToString() ?? "null") + ",";
        }
      }
      using (HashAlgorithm algorithm = SHA256.Create())
      {
        byte[] selfHash;
        selfHash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(selfHashString));
        return selfHash;
      }
    }


    /// <summary>
    /// This function does a duck type compare between the current 
    /// object and another object. It does it by calculating a hash
    /// value over all the public readable value properties and string 
    /// properties of the current object and over all readable corresponding
    /// properties of the other object.
    /// Corresponding properties are either properties with the same
    /// name and type as the source property or properties which are
    /// mapped to a property of the current object.
    /// The function returns true if the hash values are the same,
    /// otherwise false.
    /// </summary>
    /// <param name="self"></param>
    /// <param name="other"></param>
    /// <param name="mapList"></param>
    /// <param name="excludeSelect"></param>
    /// <typeparam name="TSelf"></typeparam>
    /// <typeparam name="TOther"></typeparam>
    public static bool IsEqualTo<TSelf, TOther>(this TSelf self, TOther other, List<Map> mapList = null, Func<PropertyInfo, bool> excludeSelect = null) where TSelf : class where TOther : class
    {
      List<PropertyInfo> propertyInfoList;
      string selfHashString;
      string otherHashString;
      PropertyInfo otherProperty;
      Map map;

      if(excludeSelect == null)
      {
        //
        // NOP exclude selection. 
        //
        excludeSelect = (selfObj) => { return false;};
      }

      propertyInfoList = self.GetType().GetProperties()
      .Where(item => item.CanRead)
      .ToList();

      selfHashString = string.Empty;
      otherHashString = string.Empty;

      foreach (var property in propertyInfoList)
      {
        if(excludeSelect(property))
        {
          //
          // Current property is exclude. Continue with the next.
          //
          continue;
        }

        //
        // Constrain the property collection to value types and strings.
        //
        if (property.PropertyType.IsPrimitive || property.PropertyType.IsValueType || property.PropertyType == typeof(string))
        {
          map = new Map();

          if (mapList != null)
          {
            map = mapList.Where(item => item.SourceName == property.Name).FirstOrDefault();
          }

          if (map.IsEmpty)
          {
            //
            // Default behavior.
            // Create the hash string from the current
            // property value.
            //
            selfHashString += (property.GetValue(self)?.GetHashCode().ToString() ?? "null") + ",";
          }
          else
          {
            //
            // Use the converted value to create the hash string in
            // order to make a comparsion to a converted target 
            // property possible.
            //
            selfHashString += (map.Convert(property.GetValue(self))?.GetHashCode().ToString() ?? "null") + ",";
          }

          otherProperty = null;

          //
          // Get the corresponding property from the other object.
          //
          if (map.IsEmpty)
          {
            //
            // Default behavior.
            // Use the source property name to identify the
            // target property.
            //
            otherProperty = other.GetType().GetProperties()
            .Where(prop => prop.Name == property.Name)
            .Where(prop => prop.CanRead)
            .Where(prop => prop.GetType() == property.GetType())
            .FirstOrDefault();
          }
          else
          {
            //
            // Use the mapped target name to identify the 
            // target property.
            //
            otherProperty = other.GetType().GetProperties()
            .Where(prop => prop.Name == map.TargetName)
            .Where(prop => prop.CanRead)
            .Where(prop => prop.GetType() == property.GetType())
            .FirstOrDefault();
          }

          if (otherProperty == null)
          {
            return false;
          }

          otherHashString += (otherProperty.GetValue(other)?.GetHashCode().ToString() ?? "null") + ",";
        }
      }
      using (HashAlgorithm algorithm = SHA256.Create())
      {
        byte[] selfHash;
        byte[] otherHash;
        selfHash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(selfHashString));
        otherHash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(otherHashString));
        return selfHash.SequenceEqual(otherHash);
      }
    }

    /// <summary>
    /// The function copies the public readable value property values
    /// and string values to the corresponding properties of the target
    /// object.
    /// Corresponding properties are either properties with the same
    /// name and type as the source property or properties which are
    /// mapped to a property of the current object.
    /// </summary>
    /// <param name="self"></param>
    /// <param name="other"></param>
    /// <param name="mapList"></param>
    /// <param name="excludeSelect"></param>
    /// <typeparam name="TSelf"></typeparam>
    /// <typeparam name="TOther"></typeparam>
    /// <exception cref="DataObjectExtensionException">In case a target property is missing or a conversion failed.</exception>
    /// <exception cref="System.ArgumentException">In case the 'other' object is null.</exception>
    public static void CopyTo<TSelf, TOther>(this TSelf self, TOther other, List<Map> mapList = null, Func<PropertyInfo, bool> excludeSelect = null) where TSelf : class where TOther : class
    {
      List<PropertyInfo> propertyInfoList;
      Dictionary<PropertyInfo, PropertyInfo> SourceTargetDict;
      PropertyInfo targetProperty;
      Map map;
      string methodName;
      string className;



      if(other == null)
      {
        className = MethodBase.GetCurrentMethod().DeclaringType.FullName;
        methodName = MethodBase.GetCurrentMethod().Name;
         throw new System.ArgumentException($"Argument '{other}' must not be null in function '{className + "." + methodName}'.");
      }

      if(excludeSelect == null)
      {
        //
        // NOP exclude selection. 
        //
        excludeSelect = (selfObj) => { return false;};
      }

      SourceTargetDict = new Dictionary<PropertyInfo, PropertyInfo>();
      propertyInfoList = self.GetType().GetProperties()
      .Where(item => item.CanRead)
      .ToList();

      foreach (var property in propertyInfoList)
      {
        
        if(excludeSelect(property))
        {
          //
          // Current property is exclude. Continue with the next.
          //
          continue;
        }

        //
        // Constrain the property collection to value types and strings.
        //
        if (property.PropertyType.IsPrimitive || property.PropertyType.IsValueType || property.PropertyType == typeof(string))
        {
          map = new Map();

          if (mapList != null)
          {
            map = mapList.Where(item => item.SourceName == property.Name).FirstOrDefault();
          }

          targetProperty = null;

          if (map.IsEmpty)
          {
            //
            // Default behavior.
            // Get the target property which has a name which 
            // matches with the source property name, is read
            // and writable and has the same type as the source
            // property.
            //
            targetProperty = other.GetType().GetProperties()
            .Where(tProperty => tProperty.Name == property.Name)
            .Where(tProperty => tProperty.CanRead)
            .Where(tProperty => tProperty.CanWrite)
            .Where(tProperty => tProperty.GetType() == property.GetType())
            .FirstOrDefault();
          }
          else
          {
            //
            // Look for a target property which has a name which
            // matches with the 'map.TargetName'.
            // Since there might also be a conversion skip the 
            // type check.
            //
            targetProperty = other.GetType().GetProperties()
            .Where(tProperty => tProperty.Name == map.TargetName)
            .Where(tProperty => tProperty.CanRead)
            .Where(tProperty => tProperty.CanWrite)
            .FirstOrDefault();
          }


          if (targetProperty == null)
          {
            //
            // Couldn't find a matching target property.
            // Cancel the operation.
            //
            throw new DataObjectExtensionException($"Can't find a matching target property for the source property {property.Name}. The copy operation got canceled.");
          }
          else
          {
            //
            // Add the source and the corresponding target
            // property to the dictionary.
            //
            SourceTargetDict.Add(property, targetProperty);
          }
        }
      }

      //
      // Iterate over all items in the dictionary and copy
      // the property value from the source properties to 
      // the corresponding target properties.
      //
      foreach (var dictItem in SourceTargetDict)
      {
        map = new Map();

        if (mapList != null)
        {
          map = mapList.Where(mapItem => mapItem.SourceName == dictItem.Key.Name).FirstOrDefault();
        }

        if(map.IsEmpty)
        {
          //
          // Default behavior.
          // Copy the source property value to the target property.
          //
          dictItem.Value.SetValue(other, (object) dictItem.Key.GetValue(self));
        }
        else
        {
          //
          // User the converter from the assigned mapping and 
          // copy the converted source property value to the target property.
          //
          try
          {
            dictItem.Value.SetValue(other, map.Convert(dictItem.Key.GetValue(self)));
          }
          catch(System.Exception ex)
          {
            throw new DataObjectExtensionException($"Conversion from the source property '{dictItem.Key.Name}' to the target property '{dictItem.Value.Name}' failed. Discard the target object because it might already be in a  corrupted state. See the inner exceptions for further information.", ex);
          }
        }
      }
    }

  }//END class
}//END namespace
