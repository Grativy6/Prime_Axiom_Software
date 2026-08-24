using System.Numerics;
using PrimeAxiom.Core.Machine;
using PrimeAxiom.Core.Representations;

namespace PrimeAxiom.Tests;

public sealed class PrimeMachineTests
{
    [Fact]
    public void FactorResidentProgramComposesAndReconstructsWithStepReceipts()
    {
        var basis = PrimeBasis.First(4);
        var machine = new PrimeMachine(basis, exponentWidth: 8, registerCount: 4);
        var program = new PrimeProgram(new PrimeInstruction[]
        {
            new LoadCertifiedCoordinates(0, new BigInteger[] { 2, 1, 0, 0 }, CertificateVerified: true),
            new LoadMagnitude(1, 18),
            new Compose(2, 0, 1),
            new ProjectExponent(2, 0),
            new ReconstructMagnitude(2),
        });

        var receipt = machine.Run(program);

        Assert.True(receipt.Completed);
        Assert.Null(receipt.FailedAt);
        Assert.Equal(5, receipt.Steps.Count);
        Assert.All(receipt.Steps, step => Assert.True(step.Succeeded));
        Assert.Equal(new BigInteger(216), receipt.LastScalar);
        Assert.Equal(new BigInteger(216), machine.ReadRegister(2)!.Reconstruct().Value);
        Assert.False(receipt.Steps[2].CoordinateReceipt!.UsedMagnitudeDomain);
        Assert.True(receipt.Steps[4].CoordinateReceipt!.UsedMagnitudeDomain);
    }

    [Fact]
    public void UnverifiedCertificateStopsWithoutLoadingARegister()
    {
        var machine = new PrimeMachine(PrimeBasis.First(3), exponentWidth: 4);
        var receipt = machine.Run(new PrimeProgram(new PrimeInstruction[]
        {
            new LoadCertifiedCoordinates(0, new BigInteger[] { 1, 1, 0 }, CertificateVerified: false),
            new ReconstructMagnitude(0),
        }));

        Assert.False(receipt.Completed);
        Assert.Equal(0, receipt.FailedAt);
        Assert.Single(receipt.Steps);
        Assert.Contains("not verified", receipt.Steps[0].Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Null(machine.ReadRegister(0));
    }

    [Fact]
    public void MagnitudeBasisEscapeAndExactCancelFailureRemainDistinct()
    {
        var basis = PrimeBasis.First(3);
        var escaped = new PrimeMachine(basis, exponentWidth: 4).Run(new PrimeProgram(new PrimeInstruction[]
        {
            new LoadMagnitude(0, 7),
        }));
        Assert.False(escaped.Completed);
        Assert.Equal(CoordinateFailure.BasisEscape, escaped.Steps[0].CoordinateReceipt!.Failure);
        Assert.Equal(new BigInteger(7), escaped.Steps[0].CoordinateReceipt!.UnrepresentedResidual);

        var machine = new PrimeMachine(basis, exponentWidth: 4);
        var cancel = machine.Run(new PrimeProgram(new PrimeInstruction[]
        {
            new LoadMagnitude(0, 6),
            new LoadMagnitude(1, 4),
            new Cancel(2, 0, 1),
        }));
        Assert.False(cancel.Completed);
        Assert.Equal(2, cancel.FailedAt);
        Assert.Equal(CoordinateFailure.NotDivisible, cancel.Steps[2].CoordinateReceipt!.Failure);
        Assert.Null(machine.ReadRegister(2));
    }

    [Fact]
    public void ContinuingAfterFailureKeepsTheRunFailedAndInvalidatesTheDestination()
    {
        var machine = new PrimeMachine(PrimeBasis.First(3), exponentWidth: 4);
        var receipt = machine.Run(
            new PrimeProgram(new PrimeInstruction[]
            {
                new LoadMagnitude(0, 6),
                new LoadMagnitude(1, 4),
                new LoadMagnitude(2, 2),
                new Cancel(2, 0, 1),
                new ReconstructMagnitude(2),
            }),
            stopOnFailure: false);

        Assert.False(receipt.Completed);
        Assert.Equal(3, receipt.FailedAt);
        Assert.Equal(5, receipt.Steps.Count);
        Assert.False(receipt.Steps[3].Succeeded);
        Assert.False(receipt.Steps[4].Succeeded);
        Assert.Null(machine.ReadRegister(2));
        Assert.Null(receipt.LastScalar);
    }

    [Fact]
    public void EachRunResetsItsScalarOutputBoundary()
    {
        var machine = new PrimeMachine(PrimeBasis.First(3), exponentWidth: 4);
        var first = machine.Run(new PrimeProgram(new PrimeInstruction[]
        {
            new LoadMagnitude(0, 6),
            new ReconstructMagnitude(0),
        }));
        Assert.Equal(new BigInteger(6), first.LastScalar);

        var second = machine.Run(new PrimeProgram(new PrimeInstruction[]
        {
            new LoadMagnitude(1, 4),
        }));

        Assert.True(second.Completed);
        Assert.Null(second.LastScalar);
        Assert.Null(machine.LastScalar);
    }
}
