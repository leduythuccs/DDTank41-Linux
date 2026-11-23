using System;
using Microsoft.Data.SqlClient;

namespace DAL
{
    public delegate void AsyncComandExecuteHanle(SqlCommand cmd, IAsyncResult result, object state);
}
