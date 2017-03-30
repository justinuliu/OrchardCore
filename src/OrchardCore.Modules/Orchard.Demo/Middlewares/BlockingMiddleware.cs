﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Orchard.Demo
{
    public class BlockingMiddleware
    {
        private readonly RequestDelegate _next;

        public BlockingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path == "/middleware")
            {
                await httpContext.Response.WriteAsync("middleware");
            }
            else
            {
                await _next.Invoke(httpContext);
            }
        }
    }
}