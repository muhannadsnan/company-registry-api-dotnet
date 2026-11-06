#:package Microsoft.Extensions.Identity.Core@10.0.13

using Microsoft.AspNetCore.Identity;

// Prints a password hash in the format ASP.NET Core Identity uses (PBKDF2, salted).
// Usage: dotnet run hash-password.cs -- <password>
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run hash-password.cs -- <password>");
    return 1;
}

var hasher = new PasswordHasher<object>();
Console.WriteLine(hasher.HashPassword(new object(), args[0]));
return 0;
