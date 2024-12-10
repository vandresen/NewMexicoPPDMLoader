﻿using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace LoaderLibrary.DataAccess
{
    public class DapperDataAccess : IDataAccess
    {
        public async Task SaveData<T>(string connectionString, T data, string sql)
        {
            using IDbConnection cnn = new SqlConnection(connectionString);
            await cnn.ExecuteAsync(sql, data);
        }

        public async Task<IEnumerable<T>> LoadData<T, U>(string storedProcedure, U parameters, string connectionString)
        {
            using IDbConnection cnn = new SqlConnection(connectionString);
            return await cnn.QueryAsync<T>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}