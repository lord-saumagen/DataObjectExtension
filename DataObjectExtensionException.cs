using System;
using System.Runtime.Serialization;

namespace DataObjectExtension
{
  /// <summary>
  /// Extension specific exception class.
  /// </summary>
  public class DataObjectExtensionException : System.Exception
  {
     /// <summary>
     /// Default constructor.
     /// </summary>
     /// <returns></returns>
     public DataObjectExtensionException() : base()
     {}

     /// <summary>
     /// Message constructor
     /// </summary>
     /// <param name="message"></param>
     /// <returns></returns>
     public DataObjectExtensionException(string message) : base(message)
     {}

    /// <summary>
    /// Message and inner exception constructor.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    /// <returns></returns>
    public DataObjectExtensionException(string message, Exception innerException) : base(message, innerException)
    {}

    /// <summary>
    /// Serialization constructor.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected DataObjectExtensionException(SerializationInfo info, StreamingContext context) : base(info, context)
    {}

  }
}