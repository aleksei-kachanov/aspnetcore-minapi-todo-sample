using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using WebMinRouteGroup;
using WebMinRouteGroup.Data;
using WebMinRouteGroup.Services;

namespace UnitTests;

public class TodoMoqTests
{
    // A helper that creates a ClaimsPrincipal with a known NameIdentifier so the
    // V2 endpoint handlers can resolve ownerId without returning Forbid().
    private static ClaimsPrincipal AuthenticatedUser(string userId = "test-user-id")
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task GetTodoReturnsNotFoundIfNotExists()
    {
        // Arrange
        var mock = new Mock<ITodoService>();

        mock.Setup(m => m.Find(It.Is<int>(id => id == 1)))
            .ReturnsAsync((Todo?)null);

        // Act
        var result = await TodoEndpointsV2.GetTodo(1, AuthenticatedUser(), mock.Object);

        //Assert
        Assert.IsType<Results<Ok<Todo>, NotFound, ForbidHttpResult>>(result);

        var notFoundResult = (NotFound) result.Result;

        Assert.NotNull(notFoundResult);
    }

    [Fact]
    public async Task GetAllReturnsTodosFromDatabase()
    {
        // Arrange
        var mock = new Mock<ITodoService>();
        var ownerId = "test-user-id";

        var items = new List<Todo>
        {
            new Todo { Id = 1, Title = "Test title 1", IsDone = false, OwnerId = ownerId },
            new Todo { Id = 2, Title = "Test title 2", IsDone = true,  OwnerId = ownerId }
        };

        mock.Setup(m => m.GetPaged(It.IsAny<TodoQueryParams>(), ownerId))
            .ReturnsAsync(new PagedResult<Todo>
            {
                Items = items,
                Total = items.Count,
                Page = 1,
                Size = 20
            });

        // Act
        var result = await TodoEndpointsV2.GetAllTodos(AuthenticatedUser(ownerId), mock.Object);

        //Assert
        Assert.IsType<Results<Ok<PagedResult<Todo>>, BadRequest<string>, ForbidHttpResult>>(result);

        var okResult = (Ok<PagedResult<Todo>>) result.Result;
        Assert.NotNull(okResult.Value);
        Assert.NotEmpty(okResult.Value.Items);
        Assert.Collection(okResult.Value.Items, todo1 =>
        {
            Assert.Equal(1, todo1.Id);
            Assert.Equal("Test title 1", todo1.Title);
            Assert.False(todo1.IsDone);
        }, todo2 =>
        {
            Assert.Equal(2, todo2.Id);
            Assert.Equal("Test title 2", todo2.Title);
            Assert.True(todo2.IsDone);
        });
    }

    [Fact]
    public async Task GetAllIncompletedReturnsIncompletedTodosFromDatabase()
    {
        // Arrange
        var mock = new Mock<ITodoService>();
        var ownerId = "test-user-id";

        mock.Setup(m => m.GetIncompleteTodos(ownerId))
            .ReturnsAsync(new List<Todo> {
                new Todo { Id = 1, Title = "Test title 1", IsDone = false, OwnerId = ownerId },
                new Todo { Id = 2, Title = "Test title 2", IsDone = false, OwnerId = ownerId }
            });

        // Act
        var result = await TodoEndpointsV2.GetAllIncompletedTodos(AuthenticatedUser(ownerId), mock.Object);

        //Assert
        Assert.IsType<Results<Ok<List<Todo>>, ForbidHttpResult>>(result);

        var okResult = (Ok<List<Todo>>) result.Result;
        Assert.NotNull(okResult.Value);
        Assert.NotEmpty(okResult.Value);
        Assert.Collection(okResult.Value, todo1 =>
        {
            Assert.Equal(1, todo1.Id);
            Assert.Equal("Test title 1", todo1.Title);
            Assert.False(todo1.IsDone);
        }, todo2 =>
        {
            Assert.Equal(2, todo2.Id);
            Assert.Equal("Test title 2", todo2.Title);
            Assert.False(todo2.IsDone);
        });
    }

    [Fact]
    public async Task GetTodoReturnsTodoFromDatabase()
    {
        // Arrange
        var mock = new Mock<ITodoService>();
        var ownerId = "test-user-id";

        mock.Setup(m => m.Find(It.Is<int>(id => id == 1)))
            .ReturnsAsync(new Todo { Id = 1, Title = "Test title", IsDone = false, OwnerId = ownerId });

        // Act
        var result = await TodoEndpointsV2.GetTodo(1, AuthenticatedUser(ownerId), mock.Object);

        //Assert
        Assert.IsType<Results<Ok<Todo>, NotFound, ForbidHttpResult>>(result);

        var okResult = (Ok<Todo>) result.Result;

        Assert.NotNull(okResult.Value);
        Assert.Equal(1, okResult.Value.Id);
    }

    [Fact]
    public async Task CreateTodoCreatesTodoInDatabase()
    {
        //Arrange
        var todos = new List<Todo>();
        var ownerId = "test-user-id";

        var newTodo = new TodoDto
        {
            Title = "Test title",
            Description = "Test description",
            IsDone = false
        };

        var mock = new Mock<ITodoService>();

        mock.Setup(m => m.Add(It.Is<Todo>(t => t.Title == newTodo.Title && t.Description == newTodo.Description && t.IsDone == newTodo.IsDone)))
            .Callback<Todo>(todo => todos.Add(todo))
            .Returns(Task.CompletedTask);

        //Act
        var result = await TodoEndpointsV2.CreateTodo(newTodo, AuthenticatedUser(ownerId), mock.Object);

        //Assert
        Assert.IsType<Results<Created<Todo>, ForbidHttpResult>>(result);

        var createdResult = (Created<Todo>) result.Result;

        Assert.NotNull(createdResult);
        Assert.NotNull(createdResult.Location);

        Assert.NotEmpty(todos);
        Assert.Collection(todos, todo =>
        {
            Assert.Equal("Test title", todo.Title);
            Assert.Equal("Test description", todo.Description);
            Assert.False(todo.IsDone);
        });
    }

    [Fact]
    public async Task UpdateTodoUpdatesTodoInDatabase()
    {
        //Arrange
        var ownerId = "test-user-id";
        var existingTodo = new Todo { Id = 1, Title = "Exiting test title", IsDone = false, OwnerId = ownerId };

        var updatedTodo = new UpdateTodoDto
        {
            Title = "Updated test title",
            IsDone = true
        };

        var mock = new Mock<ITodoService>();

        mock.Setup(m => m.Find(It.Is<int>(id => id == 1)))
            .ReturnsAsync(existingTodo);

        mock.Setup(m => m.Update(It.Is<Todo>(t => t.Id == existingTodo.Id && t.IsDone == updatedTodo.IsDone)))
            .Callback<Todo>(todo => existingTodo = todo)
            .Returns(Task.CompletedTask);

        //Act
        var result = await TodoEndpointsV2.UpdateTodo(updatedTodo, 1, AuthenticatedUser(ownerId), mock.Object);

        //Assert
        Assert.IsType<Results<Ok<Todo>, NotFound, ForbidHttpResult>>(result);

        var okResult = (Ok<Todo>) result.Result;

        Assert.NotNull(okResult);

        Assert.Equal("Updated test title", existingTodo.Title);
        Assert.True(existingTodo.IsDone);
    }

    [Fact]
    public async Task DeleteTodoDeletesTodoInDatabase()
    {
        //Arrange
        var ownerId = "test-user-id";
        var existingTodo = new Todo { Id = 1, Title = "Test title 1", IsDone = false, OwnerId = ownerId };
        var todos = new List<Todo> { existingTodo };

        var mock = new Mock<ITodoService>();

        mock.Setup(m => m.Find(It.Is<int>(id => id == existingTodo.Id)))
            .ReturnsAsync(existingTodo);

        mock.Setup(m => m.Remove(It.Is<Todo>(t => t.Id == 1)))
            .Callback<Todo>(t => todos.Remove(t))
            .Returns(Task.CompletedTask);

        //Act
        var result = await TodoEndpointsV2.DeleteTodo(existingTodo.Id, AuthenticatedUser(ownerId), mock.Object);

        //Assert
        Assert.IsType<Results<NoContent, NotFound, ForbidHttpResult>>(result);

        var noContentResult = (NoContent) result.Result;

        Assert.NotNull(noContentResult);
        Assert.Empty(todos);
    }
}
