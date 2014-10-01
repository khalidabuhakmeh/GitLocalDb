using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BranchDatabases.DatabaseHelpersExample;
using NPoco;

namespace BranchDatabases
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new GitLocalDb("Program");

            var database = new Database(config.OpenConnection());
            const string sql = @"if not exists (select * from sysobjects where name='Person' and xtype='U')
                        create table Person (
                            Id int IDENTITY PRIMARY KEY,
                            Name varchar(60) NULL,
                        )";

            database.Execute(sql);
            
            database.Insert(new Person { Name = "Khalid Abuhakmeh" });
            var result = database.Query<Person>("select * from Person");
            Debug.Assert(result.Any());
        }
    }

    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
