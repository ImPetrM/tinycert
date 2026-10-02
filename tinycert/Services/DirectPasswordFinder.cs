using Org.BouncyCastle.OpenSsl;

namespace tinycert.Services;

public class DirectPasswordFinder : IPasswordFinder
{
    private readonly char[] _password;
    
    public DirectPasswordFinder(string password)
    {
        _password = password.ToCharArray();
    }
    
    public char[] GetPassword()
    {
        return _password;
    }
}