using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message) : base(message, 403) { }
    }
}
