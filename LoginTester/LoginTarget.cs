namespace LoginTester;

public class LoginTarget
{
    public string Name { get; set; } = "";
    public string LoginUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>
    /// A CSS selector that only appears when successfully logged in.
    /// Used to verify login succeeded.
    /// </summary>
    public string SuccessIndicator { get; set; } = "";
}

public class LoginResult
{
    public bool Success { get; set; }
    public string FailureReason { get; set; } = "";
    public string LandedUrl { get; set; } = "";
}