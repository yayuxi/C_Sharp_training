namespace træning;
using TCPData;
using TCPExtensions;
using System.Linq;

class run
{
    /* Week 1
    AldersCheck();
    GetFizzBuzz();
    GetNameSort();*/
    
    /* Week 2
    Person AndersAnd = new Person {
        Name = "Anders And",
        Age = 30
    };
    Person JamesBond = new Person {
        Name = "James Bond",
        Age = 60
    };
    Person MrBeast = new Person { 
       Name = "Mr. Beast", 
       Age = 35 
    };
    Developer LinusTorvalds = new Developer {
        Name = "Linus Torvalds",
        Age = 50,
        FavoriteLanguage = "C++"
    };
    AndersAnd.Greet();
    JamesBond.Greet();
    MrBeast.Greet();
    LinusTorvalds.Greet();*/
    
    /* Week 3
    Contact contact = new Contact("","");
        string instructions =
            "Indtast 'add' for at tilføje en kontakt, 'remove' for at fjerne en kontakt, 'search' for at søge efter en kontakt, 'list' for at se alle kontakter eller 'exit' for at afslutte programmet.";
        Console.WriteLine("Velkommen til kontaktbogen! Her kan du tilføje, søge og se dine kontakter.");
        Console.WriteLine(instructions);
        while (true) {
            string input = Console.ReadLine();
            switch(input) {
                case "add":
                    Console.WriteLine("Indtast navnet på kontakten:\t");
                    string name = Console.ReadLine();
                    Console.WriteLine("Indtast emailen på kontakten:\t");
                    string email = Console.ReadLine();
                    contact.addContact(name, email);
                    Console.WriteLine("Kontakten er tilføjet!");
                    Console.WriteLine(instructions);
                    break;
                case "remove":
                    Console.WriteLine("Indtast navnet på kontakten du vil fjerne:\t");
                    string removeName = Console.ReadLine();
                    contact.removeContact(removeName);
                    Console.WriteLine("Kontakten er fjernet!");
                    Console.WriteLine(instructions);
                    break;
                case "search":
                    Console.WriteLine("Indtast navnet på kontakten du vil søge efter:\t");
                    string searchName = Console.ReadLine();
                    contact.searchContact(searchName);
                    Console.WriteLine(instructions);
                    break;
                case "list":
                    contact.printList();
                    Console.WriteLine(instructions);
                    break;
                case "exit":
                    contact.exitProgram();
                    break;
                default:                         
                    Console.WriteLine("Ugyldig kommando! Indtast 'add', 'search', 'list' eller 'exit'.");
                    break;
                
            }
            
        }
        
        List<Products> productList = Data.GetProducts();
        var mostExpensive = productList.OrderByDescending(p => p.Price).FirstOrDefault();
        Console.WriteLine($"The most expensive product is: {mostExpensive.Name} with a price of {mostExpensive.Price} in the category {mostExpensive.Category}");
        var filteredItems = productList.Where(p => p.Price > 1000).OrderBy(p => p.Price).GroupBy(p => p.Category);
        foreach (var group in filteredItems)
        {
            foreach (var item in group)
            {
                Console.WriteLine($"{item.Name} - {item.Price} - {item.Category}");
            }
            Console.WriteLine($"There are {group.Count()} items in the category {group.Key}");
        }
        var people = File.ReadLines("C:\\Users\\Bruger 1\\RiderProjects\\C_Sharp_training\\træning\\people.txt");

        var isPerson = people.Any(person => person.StartsWith("A"));
        Console.WriteLine($"Is there any person that starts with 'A'? {isPerson}");
        var result = people
            .Where(person => person.StartsWith("A"))
            .OrderBy(person => person);

        foreach (var person in result)
        {
            Console.WriteLine(person);
        }
        
        
        Stack<int> stack = new Stack<int>();
        Console.WriteLine(stack.IsEmpty());
        // Console.WriteLine(stack.Pop());
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        stack.Push(4);
        stack.Push(5);
        Console.WriteLine(stack.IsEmpty());
        Console.WriteLine(stack.Peek());
        Console.WriteLine(stack.Pop());
        
        Stack<string> stackList = new Stack<string>();
        stackList.Push("Fizz");
        stackList.Push("Buzz");
        stackList.Push("FizzBuzz");
        stackList.Push("Boom");
        stackList.Push("Pow");
        Console.WriteLine(stackList.IsEmpty());
        Console.WriteLine(stackList.Pop());
        Console.WriteLine(stackList.Peek());
     */
    private static void Main(string[] args)
    {
        Button button = new  Button();
        Logger logger = new Logger(button);
        Print print = new Print(button);
        button.ButtonClick();
    }
    

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
    
    private static void AldersCheck()
    {
        Console.WriteLine("Alders check!\nVenligst indtast din alder for at se om du er myndig til at stemme:\t");
        string input = Console.ReadLine();
        try {
            int age = Convert.ToInt32(input);

            if (age < 0) {
                throw new Exception("Ugyldig alder! Indtast en gyldig alder.");
            } 
            if (age > 150) {
                throw new Exception("Din alder er for høj for et menneske, indtast en realistisk alder.");
            }

            string myndig = (age < 18) ? "Du er under 18 år og derfor ikke myndig" : "Du er myndig";
            Console.WriteLine(myndig);
            // if (age < 18) {
            //     Console.WriteLine("Du er under 18 år og derfor ikke myndig");
            // } else {
            //     Console.WriteLine("Du er myndig");
            // }
        } catch (Exception e) {
            Console.WriteLine(e);
        }
    }
    
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