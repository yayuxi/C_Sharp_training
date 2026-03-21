namespace træning;


class run
{
    private static void Main(string[] args)
    {
        // AldersCheck();
        // GetFizzBuzz();
        // GetNameSort();
        // Person AndersAnd = new Person("Anders And", 30);
        // Person JamesBond = new Person("James Bond", 60);
        // Person MrBeast =  new Person("Mr. Beast", 35);
        // AndersAnd.Greet();
        // JamesBond.Greet();
        // MrBeast.Greet();
        Contact contact = new Contact("","");
        Console.WriteLine("Velkommen til kontaktbogen! Her kan du tilføje, søge og se dine kontakter.\nIndtast 'add' for at tilføje en kontakt, 'remove' for at fjerne en kontakt, 'search' for at søge efter en kontakt, 'list' for at se alle kontakter eller 'exit' for at afslutte programmet.");
        while (true) {
            string input = Console.ReadLine();
            switch(input) {
                case "add":
                    Console.WriteLine("Indtast navnet på kontakten:\t");
                    string name = Console.ReadLine();
                    Console.WriteLine("Indtast emailen på kontakten:\t");
                    string email = Console.ReadLine();
                    contact.addContact(name, email);
                    Console.WriteLine("Kontakten er tilføjet!\nIndtast 'add' for at tilføje en kontakt, 'remove' for at fjerne en kontakt, 'search' for at søge efter en kontakt, 'list' for at se alle kontakter eller 'exit' for at afslutte programmet.");
                    break;
                case "remove":
                    Console.WriteLine("Indtast navnet på kontakten du vil fjerne:\t");
                    string removeName = Console.ReadLine();
                    contact.removeContact(removeName);
                    Console.WriteLine("Kontakten er fjernet!\nIndtast 'add' for at tilføje en kontakt, 'remove' for at fjerne en kontakt, 'search' for at søge efter en kontakt, 'list' for at se alle kontakter eller 'exit' for at afslutte programmet.");
                    break;
                case "search":
                    Console.WriteLine("Indtast navnet på kontakten du vil søge efter:\t");
                    string searchName = Console.ReadLine();
                    contact.searchContact(searchName);
                    Console.WriteLine("Indtast 'add' for at tilføje en kontakt, 'remove' for at fjerne en kontakt, 'search' for at søge efter en kontakt, 'list' for at se alle kontakter eller 'exit' for at afslutte programmet.");
                    break;
                case "list":
                    contact.printList();
                    Console.WriteLine("Indtast 'add' for at tilføje en kontakt, 'remove' for at fjerne en kontakt, 'search' for at søge efter en kontakt, 'list' for at se alle kontakter eller 'exit' for at afslutte programmet.");
                    break;
                case "exit":
                    contact.exitProgram();
                    break;
                default:                         
                    Console.WriteLine("Ugyldig kommando! Indtast 'add', 'search', 'list' eller 'exit'.");
                    break;
                
            }
            
        }
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