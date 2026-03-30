using System.Text.RegularExpressions;

namespace træning;

public class Contact
{
    public string Name { get; set; }
    public string Email { get; set; }
    
    List<Contact> Contacts = new List<Contact>();

    public Contact(string name, string email) {
        Name = name;
        Email = email;
    }

    public void addContact(string name, string email) {
        try {
            checkInvalidEmail(email);
            Contacts.Add(new Contact(name, email));
            Console.WriteLine("Kontakten er tilføjet!");
        } catch (Exception e) {            
            Console.WriteLine(e.Message);
            using (StreamWriter writer = new(
                       File.Open("C:\\Users\\elias\\RiderProjects\\træning\\træning\\errors.txt", 
                           FileMode.Append, FileAccess.Write, FileShare.Write)))
            {
                writer.WriteLine($"{DateTime.Now}: {e.Message}");
            }
        }
    }

    public void removeContact(string name) {
        try {
            checkIfContactExists(name);
            Contacts.RemoveAt(Contacts.FindIndex(x => x.Name == name));
            Console.WriteLine("Kontakten er fjernet!");
        }
        catch (Exception e) {
            Console.WriteLine(e.Message);
            using (StreamWriter writer = new(
                       File.Open("C:\\Users\\elias\\RiderProjects\\træning\\træning\\errors.txt", 
                           FileMode.Append, FileAccess.Write, FileShare.Write)))
            {
                writer.WriteLine($"{DateTime.Now}: {e.Message}");
            }
        }
    }
    
    public void printList() {
        foreach (var contact in Contacts) {
            Console.WriteLine(contact.Name + "\t" + contact.Email);
        }
    }
    
    public void searchContact(string name) {
        try {
            checkIfContactExists(name);
            foreach (var contact in Contacts) {
                if (contact.Name == name) {
                    Console.WriteLine(contact.Name + "\t" + contact.Email);
                    break;
                }
            }
        }
        catch (Exception e) {
            Console.WriteLine(e.Message);
            using (StreamWriter writer = new(
                       File.Open("C:\\Users\\elias\\RiderProjects\\træning\\træning\\errors.txt", 
                           FileMode.Append, FileAccess.Write, FileShare.Write)))
            {
                writer.WriteLine($"{DateTime.Now}: {e.Message}");
            }
        }
    }
    
    public void exitProgram() {
        Console.WriteLine("Tak for at bruge vores kontaktbog! Vi ses næste gang!");
        Environment.Exit(0);
    }

    public class ContactNotFoundException : Exception {
        public ContactNotFoundException(string message) : base(message) { }
    }

    public class InvalidEmailException : Exception {
        public InvalidEmailException(string message) : base(message) {
        }
    }

    public void checkIfContactExists(string name) {
        foreach (var contact in Contacts) {
            if (contact.Name != name) {
                throw new ContactNotFoundException("Contact not found!");
            } 
        }
    }
    public void checkInvalidEmail(string email) {
        
        string pattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";

        if (!Regex.IsMatch(email, pattern))
        {
            throw new InvalidEmailException("Invalid email format!");
        }
    }
}