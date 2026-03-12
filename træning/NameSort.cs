namespace træning;

public class NameSort
{
    public static void GetNameSort()
    {
        Console.WriteLine("Indtast et vilkårligt antal navne og programmet vil sortere det for dig i alfabetisk rækkefølge:\t");
        string input = Console.ReadLine();
        try
        {
            string[] names = input.Split(' ');
            if (names.Length < 5)
            {
                throw new Exception("Der er ikke nok navne i listen til at blive sorterert.");
            }
            Array.Sort(names);
            foreach (string name in names)
            {
                Console.WriteLine(name);
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}