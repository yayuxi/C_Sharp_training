This project is a template for web scraping using .NET and Playwright. 
It provides a basic structure and example code to get you started with web scraping tasks.

In Program.cs, the URL needs to be changed to the desired web page to scrape, and if the web page has a login then those fields needs to be filled as well.

In Scraper.cs, you will find the main scraping logic at the bottom. You can modify this file to extract the specific data you need from the target website.

Then in the terminal, go to the project folder, and you'll need to run the following command to execute the scraper:

```
bash dotnet build
bash dotnet run
```

You might need to install Playwright and its dependencies if you haven't already. You can do this by running:

```
bash dotnet add package Microsoft.Playwright
bash pwsh bin/Debug/net10.0/playwright.ps1 install     
bash dotnet add package CSVHelper
```