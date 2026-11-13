using System;
using System.Globalization;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/question-answer")]
    public class QuestionAnswerController : ControllerBase
    {
        private readonly ISsasRepository _ssas;
        private readonly IAuthorizationService _auth;
        public QuestionAnswerController(ISsasRepository ssas,IAuthorizationService auth) { _ssas=ssas;_auth=auth; }
        private static string PolicyFor(int n)=> n switch {47=>"PurchaseAccess",50=>"InventoryAccess",<=24 or >40=>"SalesAccess",<=29=>"PurchaseAccess",<=35=>"InventoryAccess",_=>"ShippingAccess"};

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            if(id==null||!id.StartsWith("MDX-",StringComparison.OrdinalIgnoreCase)||!int.TryParse(id.AsSpan(4),NumberStyles.None,CultureInfo.InvariantCulture,out int n)||n<1||n>50)
                return NotFound(new {success=false,message="Không có câu hỏi " + id});
            if(!(await _auth.AuthorizeAsync(User,PolicyFor(n))).Succeeded)return Forbid();
            var rows=await _ssas.GetQuestionAnswerFromCubeAsync(n);
            Response.Headers["X-Data-Source"]="SSAS";
            return Ok(new {success=true,id=$"MDX-{n:00}",data=rows});
        }
    }
}
