﻿using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Hl7.Fhir.Support;
using Hl7.Fhir.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Hl7.Fhir.Source
{
    public class TerminologyTests : IClassFixture<ValidationFixture>
    {            
        private IResourceResolver _resolver;

        public TerminologyTests(ValidationFixture fixture)
        {
            _resolver = fixture.Resolver;
        }

        [Fact]
        public void ExpansionOfDefine()
        {
            var issueTypeVs = _resolver.ResolveByCanonicalUri("http://hl7.org/fhir/ValueSet/issue-type").DeepCopy() as ValueSet;
            Assert.False(issueTypeVs.HasExpansion);

            // Wipe the version so we don't have to update our tests all the time
            issueTypeVs.CodeSystem.Version = null;

            var expander = new ValueSetExpander();

            expander.Expand(issueTypeVs);

            Assert.True(issueTypeVs.HasExpansion);
            var id = issueTypeVs.Expansion.Identifier;
            Assert.NotNull(id);
            Assert.False(issueTypeVs.Expansion.Parameter.Any(c => c.Name == "version"));

            Assert.True(issueTypeVs.CodeInExpansion("security", "http://hl7.org/fhir/issue-type"));
            Assert.True(issueTypeVs.CodeInExpansion("expired", "http://hl7.org/fhir/issue-type"));
            Assert.Equal(29, issueTypeVs.ExpansionSize());
            Assert.Equal(issueTypeVs.ExpansionSize(), issueTypeVs.Expansion.Total);

            var trans = issueTypeVs.FindInExpansion("transient", "http://hl7.org/fhir/issue-type");
            Assert.NotNull(trans);
            Assert.NotNull(trans.FindCode("exception"));

            // Now, make this a versioned system
            issueTypeVs.CodeSystem.Version = "3.14";
            expander.Expand(issueTypeVs);
            Assert.NotEqual(id, issueTypeVs.Expansion.Identifier);
            Assert.Equal(29, issueTypeVs.ExpansionSize());

            var versionParam = issueTypeVs.Expansion.Parameter.Single(c => c.Name == "version");
            Assert.Equal("http://hl7.org/fhir/ValueSet/issue-type?version=3.14", ((FhirUri)versionParam.Value).Value);
        }


        [Fact]
        public void ExpansionOfComposeInclude()
        {
            var testVs = _resolver.ResolveByCanonicalUri("http://hl7.org/fhir/ValueSet/marital-status").DeepCopy() as ValueSet;
            Assert.False(testVs.HasExpansion);

            var expander = new ValueSetExpander(new ValueSetExpanderSettings { ValueSetSource = _resolver });
            expander.Expand(testVs);
            Assert.Equal(11, testVs.ExpansionSize());
        }


        [Fact]
        public void ExpansionOfComposeImport()
        {
            var testVs = _resolver.ResolveByCanonicalUri("http://hl7.org/fhir/ValueSet/referencerange-meaning").DeepCopy() as ValueSet;
            Assert.False(testVs.HasExpansion);

            var expander = new ValueSetExpander(new ValueSetExpanderSettings { ValueSetSource = _resolver });
            Assert.Throws<ValueSetExpansionTooBigException>( () => expander.Expand(testVs) );

            expander.Settings.MaxExpansionSize = 1000;
            expander.Expand(testVs);
            Assert.Equal(937, testVs.ExpansionSize());
        }

        [Fact]
        public void TermServiceLoopupTest()
        {
            var svc = new LocalTerminologyServer(_resolver);

            var result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/data-absent-reason", "NaN", "http://hl7.org/fhir/data-absent-reason");
            Assert.True(result.Success);

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/data-absent-reason", "NaNX", "http://hl7.org/fhir/data-absent-reason");
            Assert.False(result.Success);

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/data-absent-reason", "NaN", "http://hl7.org/fhir/data-absent-reason", display: "Not a Number");
            Assert.True(result.Success);

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/data-absent-reason", "NaN", "http://hl7.org/fhir/data-absent-reason", display: "Not any Number");
            Assert.False(result.Success);

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/v3-AcknowledgementDetailCode", "_AcknowledgementDetailNotSupportedCode",
                "http://hl7.org/fhir/v3/AcknowledgementDetailCode");
            Assert.False(result.Success);

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/v3-AcknowledgementDetailCode", "_AcknowledgementDetailNotSupportedCode",
                "http://hl7.org/fhir/v3/AcknowledgementDetailCode", abstractAllowed: true);
            Assert.True(result.Success);

            // This is a valueset with a compose, but it has been expanded in the zip, so this will work
            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/yesnodontknow", "Y", "http://hl7.org/fhir/v2/0136");
            Assert.True(result.Success);

            // But this won't, it's also a composition, but without expansion - the local term server won't help you here
            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/allergyintolerance-substance-code", "160244002", "http://snomed.info/sct");
            Assert.False(result.Success);
            Assert.Single(result.Where(type: OperationOutcome.IssueType.NotSupported));
        }
    }
}
