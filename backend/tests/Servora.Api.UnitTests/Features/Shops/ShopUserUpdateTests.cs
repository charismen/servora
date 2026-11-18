using Microsoft.AspNetCore.Identity;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Shops;
using Xunit;

namespace Servora.Api.UnitTests.Features.Shops;

public class ShopUserUpdateTests
{
    [Fact]
    public void UpdateShopUserRequest_ShouldContainEmailAndPassword()
    {
        var request = new ShopUserEndpoints.UpdateShopUserRequest(
            FirstName: "John",
            LastName: "Doe",
            Email: "john.doe@example.com",
            Password: "TemporaryPassword123!",
            EmploymentType: "Freelance");

        Assert.Equal("John", request.FirstName);
        Assert.Equal("Doe", request.LastName);
        Assert.Equal("john.doe@example.com", request.Email);
        Assert.Equal("TemporaryPassword123!", request.Password);
        Assert.Equal("Freelance", request.EmploymentType);
    }

    [Fact]
    public void TemporaryPassword_CanBeHashedAndVerified()
    {
        var user = new ApplicationUser
        {
            UserName = "tech@example.com",
            Email = "tech@example.com"
        };

        var hasher = new PasswordHasher<ApplicationUser>();
        var temporaryPassword = "SecretTempPassword!2026";
        var hash = hasher.HashPassword(user, temporaryPassword);
        user.PasswordHash = hash;

        var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, temporaryPassword);
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        var wrongPasswordResult = hasher.VerifyHashedPassword(user, user.PasswordHash, "WrongPassword");
        Assert.Equal(PasswordVerificationResult.Failed, wrongPasswordResult);
    }
}

