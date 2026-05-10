using System;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using TruePath;

namespace PackagesProps.Infrastructure;

public static class ExceptionUtil
{
    public static void ThrowIfNotDirectory(this AbsolutePath path, IFileSystem? fileSystem = null,
        [CallerArgumentExpression(nameof(path))] string? caller = null)
    {
        if (path.DirectoryExists(fileSystem))
            return;

        throw new InvalidOperationException($"Path {path} is not a valid directory");
    }
}