using MiniHttpServer.Frimework.Core;
using MiniHttpServer.Frimework.Core.Atributes;
using MiniHttpServer.Frimework.Core.HttpResponse;
using MiniHttpServer.Frimework.Settings;
using MiniHttpServer.Model;
using MyORMLibrary;

namespace MiniHttpServer.Endpoints
{
    [Endpoint]
    internal sealed class UserEndpoint : EndpointBase
    {
        private readonly ORMContext _context;

        public UserEndpoint()
        {

            _context = new ORMContext(Singleton.GetInstance().Settings.ConectionString);
        }


        [HttpGet("user")]
        public IActionResult GetUsers()
        {
            var users = _context.ReadByAll<User>("Users");
            return Json(users);
        }


        [HttpGet("user/{id}")]
        public IActionResult GetUserById(int id)
        {
            var user = _context.ReadById<User>(id, "Users");
            if (user is null)
                return NotFound("User not found");

            return Json(user);
        }
        [HttpPost("user/create")]
        public IActionResult CreateUser(User user)
        {
            var created = _context.Create(user, "Users");
            return Json(created);
        }

        [HttpPost("user/update/{id}")]
        public IActionResult UpdateUser(int id, User user)
        {
            var existing = _context.ReadById<User>(id, "Users");
            if (existing is null)
                return NotFound("User not found");

            _context.Update(id, user, "Users");
            return Json(new { message = "User updated successfully" });
        }

        [HttpPost("user/delete/{id}")]
        public IActionResult DeleteUser(int id)
        {
            var existing = _context.ReadById<User>(id, "Users");
            if (existing is null)
                return NotFound("User not found");

            _context.Delete(id, "Users");
            return Json(new { message = "User deleted successfully" });
        }

        private IActionResult NotFound(string message)
        {
            Context.Response.StatusCode = 404;
            return Json(new { message });
        }
    }
}
