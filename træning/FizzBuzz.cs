namespace træning;

public class FizzBuzz
{
    public static void GetFizzBuzz()
    {
        Console.WriteLine("Det er tid til FIZZBUZZ!!! woo hoo\n Vælg et tal mellem 1 og 100 og se hvad resultatet bliver:\t");
        string input = Console.ReadLine();
        try
        {
            int number = Convert.ToInt32(input);
            
            if (number < 1 || number > 100) {
                throw new Exception("Ugyldig tal! Indtast et tal mellem 1 og 100.");
            }
            
            if (number % 3 == 0 && number % 5 == 0)
            {
                Console.WriteLine("FizzBuzz");
            }
            else if (number % 5 == 0)
            {
                Console.WriteLine("Buzz");
            }
            else if (number % 3 == 0)
            {
                Console.WriteLine("Fizz");
            }
            else
            {
                Console.WriteLine($"{number}");
            }
        } catch (Exception e) {
            Console.WriteLine(e);
        }
    }

    /*
    public static string GetFizzBuzz2(int number)
    {
        if (number % 3 == 0 && number % 5 == 0)
        {
            return "FizzBuzz";
        }
        else if (number % 5 == 0)
        {
            return "Buzz";
        }
        else if (number % 3 == 0)
        {
            return "Fizz";
        }
        else
        {
            return $"{number}";
        }
    }*/
}