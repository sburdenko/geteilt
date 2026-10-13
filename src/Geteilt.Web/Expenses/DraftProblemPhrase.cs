using System.Text;

namespace Geteilt.Web.Expenses;

public static class DraftProblemPhrase
{
    /// <summary>
    /// The translation key for a problem. The enum names are the API's error codes in Pascal case,
    /// so turning one into the other keeps a single phrase serving both an unfinished form and a
    /// rejection that came back from the server.
    /// </summary>
    public static string KeyFor(DraftProblem problem)
    {
        string name = problem.ToString();
        StringBuilder key = new("problem.", name.Length + 10);

        for (int at = 0; at < name.Length; at++)
        {
            if (at > 0 && char.IsUpper(name[at]))
            {
                key.Append('_');
            }

            key.Append(char.ToLowerInvariant(name[at]));
        }

        return key.ToString();
    }
}
