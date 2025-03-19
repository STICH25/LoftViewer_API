using System;
using System.Text.RegularExpressions;
namespace LoftViewer.Utilities;

public class EmailAndPasswordValidation
{
    public bool IsValidEmail(string email)
    {
        if(string.IsNullOrEmpty(email))
            return false;
        string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, pattern);
    }
    
    public bool IsValidPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;
        // Regex to check password: 8-16 chars, at least one uppercase, one digit, and one special character
        string pattern = @"^(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,16}$";
        return Regex.IsMatch(password, pattern);
    }
}