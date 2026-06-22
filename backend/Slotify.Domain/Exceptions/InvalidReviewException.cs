namespace Slotify.Domain.Exceptions;

/// <summary>La valoración no es válida (fuera del rango 1–5). HTTP 400.</summary>
public class InvalidReviewException(string message) : Exception(message);
