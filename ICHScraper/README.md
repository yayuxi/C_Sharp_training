Go to the project directory and run the following commands to install the required dependencies:
You need Powershell installed on your system to run this project. You can download it with the command
```
winget install Microsoft.PowerShell.
```

Then run the command:
```
dotnet add package Microsoft.Playwright
```
This will install the Microsoft.Playwright package, which is required for the project to run. We then need to install the CSV helper package to handle CSV file operations. Run the following command:
```
dotnet add package CsvHelper
```
This will install the CsvHelper package, which is used for reading and writing CSV files in the project. After running these commands, you should have all the necessary dependencies installed to run the ICHScraper project. You can then proceed to run the project using the command:
```
dotnet build
dotnet run
```
This will build and run the project, allowing you to scrape data from the ICH website and save it to a CSV file. Make sure to follow any additional instructions or configurations that may be required for the project to run successfully.