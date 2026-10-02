using Spectre.Console;

namespace tinycert;

public class TinyCertUi
{
    public void PrintVersion(string version)
    {
        var escapedPrompt = Markup.Escape(version);
        AnsiConsole.MarkupLine($"[bold green]TinyCert v{escapedPrompt}[/]");
    }
    
    public void PrintStepHeader(string stepName)
    {
        var escapedPrompt = Markup.Escape(stepName);
        AnsiConsole.MarkupLine($"[bold yellow]Step: {escapedPrompt}[/]");
    }
    
    public void PrintStepLine(string message)
    {
        var escapedPrompt = Markup.Escape(message);
        AnsiConsole.MarkupLine($"[grey]{escapedPrompt}[/]");
    }
    
    public void PrintStepKeyValue(string key, string value)
    {
        var escapedKey = Markup.Escape(key);
        var escapedValue = Markup.Escape(value);
        AnsiConsole.MarkupLine($"[grey]{escapedKey}: [/][white bold]{escapedValue}[/]");
    }
    
    public void PrintError(string message)
    {
        var escapedMessage = Markup.Escape(message);
        AnsiConsole.MarkupLine($"[bold red]Error: {escapedMessage}[/]");
    }
    
    public void PrintSuccess(string message)
    {
        var escapedMessage = Markup.Escape(message);
        AnsiConsole.MarkupLine($"[bold green]Success: {escapedMessage}[/]");
    }
    
    public void PrintLine(string message, bool newLine = true)
    {
        var escapedMessage = Markup.Escape(message);
        if(newLine)
        {
            AnsiConsole.MarkupLine(escapedMessage);
        }
        else
        {
            AnsiConsole.Markup(escapedMessage);
        }
    }
    
    public void NewLine()
    {
        AnsiConsole.WriteLine();
    }
    
    public string InlinePrompt(string prompt)
    {
        var escapedPrompt = Markup.Escape(prompt);
        var formattedPrompt = $"[grey]{escapedPrompt}[/]:";
        return AnsiConsole.Ask<string>(formattedPrompt);
    }
    
    public string InlinePasswordPrompt(string prompt)
    {
        var escapedPrompt = Markup.Escape(prompt);
        var password = new TextPrompt<string>($"[grey]{escapedPrompt}[/]:")
            .Secret();
  
        return AnsiConsole.Prompt(password);
    }
    
    public void PrintFilledPrompt(string prompt, string value)
    {
        var escapedPrompt = Markup.Escape(prompt);
        var escapedValue = Markup.Escape(value);
        AnsiConsole.MarkupLine($"[grey]{escapedPrompt}[/]: {escapedValue}");
    }
}