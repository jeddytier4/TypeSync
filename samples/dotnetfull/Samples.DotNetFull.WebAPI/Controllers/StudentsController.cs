﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Samples.DotNetFull.ViewModels;

namespace Samples.DotNetFull.WebAPI.Controllers
{
    [RoutePrefix("api/students")]
    public class StudentsController : ApiController
    {
        // GET: api/students
        [HttpGet]
        public IEnumerable<Student> Get()
        {
            return new List<Student>()
            {
                new Student()
                {
                    Id = 1,
                    FirstName = "John",
                    LastName = "Wick",
                    DateOfBirth = new DateTime(1980, 1, 1),
                    YearOfGraduation = DateTime.Now
                },
                new Student()
                {
                    Id = 2,
                    FirstName = "Molly",
                    LastName = "Wilkins",
                    DateOfBirth = new DateTime(1989, 1, 1),
                    YearOfGraduation = DateTime.Now.AddYears(-9)
                }
            };
        }

        // GET api/students/5
        [HttpGet]
        [Route("{id}")]
        public Student Get(long id)
        {
            return new Student()
            {
                Id = 1,
                FirstName = "John",
                LastName = "Wick",
                DateOfBirth = new DateTime(1980, 1, 1),
                YearOfGraduation = DateTime.Now
            };
        }

        // POST api/students
        [HttpPost]
        public void Post([FromBody]Student student)
        {
            // persist
        }

        // PUT api/students/5
        [HttpPut]
        [Route("{id}")]
        public void Put(long id, [FromBody]Student student)
        {
            // persist
        }

        // DELETE api/students/5
        [HttpDelete]
        [Route("{id}")]
        public void Delete(long id)
        {
            // delete from store
        }
    }
}