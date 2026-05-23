namespace OrderApi.Exceptions;

public sealed class TransientShippingException(string message) : Exception(message);