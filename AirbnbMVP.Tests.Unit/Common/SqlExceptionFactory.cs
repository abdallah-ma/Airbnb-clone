using Microsoft.Data.SqlClient;
using System.Reflection;

namespace AirbnbMVP.Tests.Unit.Common;

/// <summary>
/// Builds a real <see cref="SqlException"/> carrying a chosen server error number, so
/// tests can exercise the service's deadlock (error 1205) translation.
/// <para>
/// Neither <see cref="SqlException"/> nor <see cref="SqlError"/> has a public constructor,
/// so the error graph is assembled by reflection. <see cref="SqlError"/> argument mapping is
/// driven by parameter name so the helper survives constructor-arity changes between
/// Microsoft.Data.SqlClient versions.
/// </para>
/// </summary>
internal static class SqlExceptionFactory
{
    public static SqlException Create(int number, string message = "simulated sql error")
    {
        var collection = BuildErrorCollection(number, message);

        var ctor = typeof(SqlException)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(c =>
            {
                var p = c.GetParameters();
                return p.Length == 4
                       && p[0].ParameterType == typeof(string)
                       && p[1].ParameterType == typeof(SqlErrorCollection);
            })
            ?? throw new InvalidOperationException("SqlException(string, SqlErrorCollection, Exception, Guid) not found.");

        return (SqlException)ctor.Invoke([message, collection, null, Guid.Empty]);
    }

    private static SqlErrorCollection BuildErrorCollection(int number, string message)
    {
        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;

        var errorCtor = typeof(SqlError)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("SqlError constructor not found.");

        var error = (SqlError)errorCtor.Invoke(BuildArgs(errorCtor.GetParameters(), number, message));

        typeof(SqlErrorCollection)
            .GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [typeof(SqlError)], null)!
            .Invoke(collection, [error]);

        return collection;
    }

    private static object?[] BuildArgs(ParameterInfo[] parameters, int number, string message)
    {
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var name = parameters[i].Name ?? string.Empty;
            var type = parameters[i].ParameterType;

            args[i] = name switch
            {
                _ when name.Contains("InfoNumber", StringComparison.OrdinalIgnoreCase) => number,
                _ when name.Contains("ErrorState", StringComparison.OrdinalIgnoreCase) => (byte)1,
                _ when name.Contains("ErrorClass", StringComparison.OrdinalIgnoreCase) => (byte)0,
                _ when name.Contains("ErrorMessage", StringComparison.OrdinalIgnoreCase) => message,
                _ when name.Contains("Server", StringComparison.OrdinalIgnoreCase) => "test-server",
                _ when name.Contains("Procedure", StringComparison.OrdinalIgnoreCase) => "test-proc",
                _ when name.Contains("LineNumber", StringComparison.OrdinalIgnoreCase) => 1,
                _ when name.Contains("Win32", StringComparison.OrdinalIgnoreCase) => 0,
                _ when name.Contains("ErrorCode", StringComparison.OrdinalIgnoreCase) => 0,
                _ when type == typeof(byte[]) => Array.Empty<byte>(),
                _ when type == typeof(Guid) => Guid.Empty,
                _ when type == typeof(CancellationToken) => CancellationToken.None,
                _ => type.IsValueType ? Activator.CreateInstance(type) : null
            };
        }

        return args;
    }
}
