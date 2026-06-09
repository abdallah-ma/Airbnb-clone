using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.Exceptions
{
    public class ConflictException : AppException
    {
        public ConflictException(string message) : base(message, 409)
        {
        }
    }
}
