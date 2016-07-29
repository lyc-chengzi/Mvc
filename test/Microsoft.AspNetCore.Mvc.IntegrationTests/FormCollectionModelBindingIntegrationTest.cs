// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.IntegrationTests
{
    public class FormCollectionModelBindingIntegrationTest
    {
        private class Person
        {
            public Address Address { get; set; }
        }

        private class Address
        {
            public int Zip { get; set; }

            [ModelBinder(Name = "Files")]
            public IFormFileCollection FileCollection { get; set; }
        }

        [Fact]
        public async Task BindProperty_WithData_WithEmptyPrefix_GetsBound()
        {
            // Arrange
            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo(),
                ParameterType = typeof(Person)
            };

            var data = "Some Data Is Better Than No Data.";
            var testContext = ModelBindingTestHelper.GetTestContext(
                request =>
                {
                    request.QueryString = QueryString.Create("Address.Zip", "12345");
                    UpdateRequest(request, data, "Address.Files");
                });

            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert

            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var boundPerson = Assert.IsType<Person>(modelBindingResult.Model);
            Assert.NotNull(boundPerson.Address);
            var formFileCollection = Assert.IsAssignableFrom<IFormFileCollection>(boundPerson.Address.FileCollection);
            var file = Assert.Single(formFileCollection);
            Assert.Equal("form-data; name=Address.Files; filename=text.txt", file.ContentDisposition);
            var reader = new StreamReader(file.OpenReadStream());
            Assert.Equal(data, reader.ReadToEnd());

            // ModelState
            Assert.True(modelState.IsValid);
            Assert.Equal(2, modelState.Count);

            var entry = Assert.Single(modelState, e => e.Key == "Address.Zip").Value;
            Assert.Equal("12345", entry.AttemptedValue);
            Assert.Equal("12345", entry.RawValue);
            Assert.Single(modelState, e => e.Key == "Address.Files");
        }

        private class Car1
        {
            public string Name { get; set; }

            public FormFileCollection Specs { get; set; }
        }

        [Fact]
        public async Task BindProperty_WithData_WithPrefix_GetsBound()
        {
            // Arrange
            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "p",
                BindingInfo = new BindingInfo(),
                ParameterType = typeof(Car1)
            };

            var data = "Some Data Is Better Than No Data.";
            var testContext = ModelBindingTestHelper.GetTestContext(
                request =>
                {
                    request.QueryString = QueryString.Create("p.Name", "Accord");
                    UpdateRequest(request, data, "p.Specs");
                });

            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert

            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var car = Assert.IsType<Car1>(modelBindingResult.Model);
            Assert.NotNull(car.Specs);
            var file = Assert.Single(car.Specs);
            Assert.Equal("form-data; name=p.Specs; filename=text.txt", file.ContentDisposition);
            var reader = new StreamReader(file.OpenReadStream());
            Assert.Equal(data, reader.ReadToEnd());

            // ModelState
            Assert.True(modelState.IsValid);
            Assert.Equal(2, modelState.Count);

            var entry = Assert.Single(modelState, e => e.Key == "p.Name").Value;
            Assert.Equal("Accord", entry.AttemptedValue);
            Assert.Equal("Accord", entry.RawValue);

            Assert.Single(modelState, e => e.Key == "p.Specs");
        }

        [Fact]
        public async Task BindParameter_WithData_GetsBound()
        {
            // Arrange
            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo
                {
                    // Setting a custom parameter prevents it from falling back to an empty prefix.
                    BinderModelName = "CustomParameter",
                },
                ParameterType = typeof(IFormCollection)
            };

            var data = "Some Data Is Better Than No Data.";
            var testContext = ModelBindingTestHelper.GetTestContext(
                request =>
                {
                    UpdateRequest(request, data, "CustomParameter");
                });

            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert
            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var formCollection = Assert.IsAssignableFrom<IFormCollection>(modelBindingResult.Model);
            var file = Assert.Single(formCollection.Files);
            Assert.NotNull(file);
            Assert.Equal("form-data; name=CustomParameter; filename=text.txt", file.ContentDisposition);
            var reader = new StreamReader(file.OpenReadStream());
            Assert.Equal(data, reader.ReadToEnd());

            // ModelState
            Assert.True(modelState.IsValid);
            Assert.Empty(modelState);
        }

        [Fact]
        public async Task BindParameter_NoData_BindsWithEmptyCollection()
        {
            // Arrange
            var argumentBinder = ModelBindingTestHelper.GetArgumentBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo
                {
                    BinderModelName = "CustomParameter",
                },
                ParameterType = typeof(IFormCollection)
            };

            // No data is passed.
            var testContext = ModelBindingTestHelper.GetTestContext();

            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await argumentBinder.BindModelAsync(parameter, testContext);

            // Assert

            // ModelBindingResult
            var collection = Assert.IsAssignableFrom<IFormCollection>(modelBindingResult.Model);

            // ModelState
            Assert.True(modelState.IsValid);
            Assert.Empty(modelState);

            // FormCollection
            Assert.Empty(collection);
            Assert.Empty(collection.Files);
            Assert.Empty(collection.Keys);
        }

        private void UpdateRequest(HttpRequest request, string data, string name)
        {
            const string fileName = "text.txt";
            var fileCollection = new FormFileCollection();
            var formCollection = new FormCollection(new Dictionary<string, StringValues>(), fileCollection);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(data));

            request.Form = formCollection;
            request.ContentType = "multipart/form-data; boundary=----WebKitFormBoundarymx2fSWqWSd0OxQqq";
            request.Headers["Content-Disposition"] = $"form-data; name={name}; filename={fileName}";
            fileCollection.Add(new FormFile(memoryStream, 0, data.Length, name, fileName)
            {
                Headers = request.Headers
            });
        }
    }
}