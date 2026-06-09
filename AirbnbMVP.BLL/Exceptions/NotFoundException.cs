using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.Exceptions
{
    public class NotFoundException : AppException
    {

        public NotFoundException(string message) : base(message , 404)
        {

        }

    }
}
