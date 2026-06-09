using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.Exceptions
{
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message) : base(message, 401) { }
    }
}
