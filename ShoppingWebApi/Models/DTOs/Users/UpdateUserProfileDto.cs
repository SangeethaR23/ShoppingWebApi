using System;

namespace ShoppingWebApi.Models.DTOs.Users
{
    public class UpdateUserProfileDto
    {
 
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public DateTime? DateOfBirth { get;  set; }
    }
}