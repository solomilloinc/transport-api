using Xunit;
using Moq;
using Transport.Business.CustomerBusiness;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel;
using FluentAssertions;

namespace Transport.Tests.CustomerBusinessTests;

public class CustomerBusinessTest : TestBase
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly CustomerBusiness _customerBusiness;

    public CustomerBusinessTest()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _customerBusiness = new CustomerBusiness(_mockContext.Object);
    }

    [Fact]
    public async Task Create_ShouldReturnFailure_WhenCustomerAlreadyExists()
    {
        // Arrange

        var dto = new CustomerCreateRequestDto(
            FirstName: "John",
            LastName: "Doe",
            Email: "john@example.com",
            DocumentNumber: "12345678",
            Phone1: "121212121",
            null
        );

        var data = new List<Customer> {
            new Customer { DocumentNumber = "12345678",
                Email = "test@gmail.com",
                FirstName = "test",
                LastName = "Test",
                Phone1 = "1212121",
                Status = EntityStatusEnum.Active,
                CreatedBy = "System",
                CreatedDate = DateTime.UtcNow,
            }
        };

        _mockContext.Setup(x => x.Customers)
            .Returns(GetQueryableMockDbSet(data).Object);

        var result = await _customerBusiness.Create(dto);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CustomerError.AlreadyExists);
    }

    [Fact]
    public async Task Create_ShouldAddCustomer_WhenCustomerDoesNotExist()
    {
        // Arrange
        var dto = new CustomerCreateRequestDto(
           FirstName: "John",
           LastName: "Doe",
           Email: "john@example.com",
           DocumentNumber: "12345678",
           Phone1: "121212121",
           null
       );

        var customers = new List<Customer>();
        _mockContext.Setup(x => x.Customers).Returns(GetQueryableMockDbSet(customers).Object);
        SetupSaveChangesWithOutboxAsync(_mockContext);

        // Act
        var result = await _customerBusiness.Create(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        customers.Count.Should().Be(1);
    }

    [Fact]
    public async Task Delete_ShouldReturnFailure_WhenCustomerNotFound()
    {
        // Arrange
        _mockContext.Setup(x => x.Customers.FindAsync(It.IsAny<int>()))
            .ReturnsAsync((Customer)null);

        // Act
        var result = await _customerBusiness.Delete(1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(CustomerError.NotFound, result.Error);
    }

    [Fact]
    public async Task Delete_ShouldSoftDeleteCustomer_WhenFound()
    {
        // Arrange
        var customer = new Customer { CustomerId = 1 };

        _mockContext.Setup(x => x.Customers.FindAsync(1))
            .ReturnsAsync(customer);
        _mockContext.Setup(x => x.SaveChangesWithOutboxAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _customerBusiness.Delete(1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(EntityStatusEnum.Deleted, customer.Status);
    }

    [Fact]
    public async Task Update_ShouldReturnFailure_WhenCustomerNotFound()
    {
        _mockContext.Setup(x => x.Customers.FindAsync(It.IsAny<int>()))
            .ReturnsAsync((Customer)null);

        var dto = new CustomerUpdateRequestDto(FirstName: "Updated", LastName: "Name", Email: "test@gmail.com", "1212121", null);

        var result = await _customerBusiness.Update(1, dto);

        Assert.False(result.IsSuccess);
        Assert.Equal(CustomerError.NotFound, result.Error);
    }

    [Fact]
    public async Task Update_ShouldModifyCustomer_WhenFound()
    {
        var customer = new Customer { CustomerId = 1, FirstName = "Old", LastName = "Name" };

        _mockContext.Setup(x => x.Customers.FindAsync(1))
            .ReturnsAsync(customer);
        _mockContext.Setup(x => x.SaveChangesWithOutboxAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = new CustomerUpdateRequestDto(FirstName: "New", LastName: "Name", Email: "test@gmail.com", Phone1: "12145354", null);

        var result = await _customerBusiness.Update(1, dto);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", customer.FirstName);
    }

    [Fact]
    public async Task UpdateStatus_ShouldReturnFailure_WhenCustomerNotFound()
    {
        _mockContext.Setup(x => x.Customers.FindAsync(It.IsAny<int>()))
            .ReturnsAsync((Customer)null);

        var result = await _customerBusiness.UpdateStatus(1, EntityStatusEnum.Active);

        Assert.False(result.IsSuccess);
        Assert.Equal(CustomerError.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateStatus_ShouldUpdateStatus_WhenCustomerFound()
    {
        var customer = new Customer { CustomerId = 1, Status = EntityStatusEnum.Inactive };

        _mockContext.Setup(x => x.Customers.FindAsync(1))
            .ReturnsAsync(customer);
        _mockContext.Setup(x => x.SaveChangesWithOutboxAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _customerBusiness.UpdateStatus(1, EntityStatusEnum.Active);

        Assert.True(result.IsSuccess);
        Assert.Equal(EntityStatusEnum.Active, customer.Status);
    }
}
