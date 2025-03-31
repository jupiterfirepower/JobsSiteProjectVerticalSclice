using System.Text.Json.Serialization;

namespace Jobs.Dto.Request;

public class UserDto
{
    [JsonPropertyName("email")]
    public string Email { get; set; }
    [JsonPropertyName("password")]
    public string Password { get; set; }

    [JsonPropertyName("firstname")] 
    public string FirstName { get; set; } = null;
    
    [JsonPropertyName("lastname")]
    public string LastName { get; set; } = null;
}