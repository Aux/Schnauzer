using System.Globalization;

namespace Schnauzer;

// There's nothing like CultureInfo.TryParse so I gotta do this mess
public static class CultureHelper
{
    public static bool TryCreate(string cultureCode, out CultureInfo cultureOut)
    {
        try
        {
            cultureOut = new CultureInfo(cultureCode, false);
            return true;
        } catch (CultureNotFoundException)
        {
            cultureOut = null;
        }

        return false;
    }
}
