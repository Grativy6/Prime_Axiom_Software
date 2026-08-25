`default_nettype none

module formal_binexp_core #(
    parameter integer E2W=2, parameter integer T2=3,
    parameter integer E3W=2, parameter integer T3=2,
    parameter integer E5W=1, parameter integer T5=1,
    parameter integer E7W=1, parameter integer T7=1
)(
    input wire zero_a,input wire zero_b,
    input wire [E2W-1:0] a2,input wire [E2W-1:0] b2,
    input wire [E3W-1:0] a3,input wire [E3W-1:0] b3,
    input wire [E5W-1:0] a5,input wire [E5W-1:0] b5,
    input wire [E7W-1:0] a7,input wire [E7W-1:0] b7
);
    wire cz, xz, mz, jz;
    wire [E2W-1:0] c2,x2,m2,j2;
    wire [E3W-1:0] c3,x3,m3,j3;
    wire [E5W-1:0] c5,x5,m5,j5;
    wire [E7W-1:0] c7,x7,m7,j7;
    wire [3:0] saturated;
    wire exact, divides;
    wire [2:0] valuation;
    wire valuation_valid,valuation_infinite;
    wire [1:0] valuation_prime = 2'b10;
    wire [E2W:0] sum2={1'b0,a2}+{1'b0,b2};
    wire [E3W:0] sum3={1'b0,a3}+{1'b0,b3};
    wire [E5W:0] sum5={1'b0,a5}+{1'b0,b5};
    wire [E7W:0] sum7={1'b0,a7}+{1'b0,b7};
    wire under_any=(a2<b2)||(a3<b3)||(a5<b5)||(a7<b7);
    wire invalid=zero_b||(!zero_a&&under_any);

    pa_binexp_compose_s4 #(.E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),.E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)) compose(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.zero_y(cz),.y2(c2),.y3(c3),.y5(c5),.y7(c7),.saturated(saturated));
    pa_binexp_cancel_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W)) cancel(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.zero_y(xz),.y2(x2),.y3(x3),.y5(x5),.y7(x7),.exact(exact));
    pa_binexp_meet_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W)) meet(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.zero_y(mz),.y2(m2),.y3(m3),.y5(m5),.y7(m7));
    pa_binexp_join_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W)) u_join(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.zero_y(jz),.y2(j2),.y3(j3),.y5(j5),.y7(j7));
    pa_binexp_divides_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W)) divq(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.divides(divides));
    pa_binexp_valuation_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W),.KW(3)) valq(
        .zero(zero_a),.e2(a2),.e3(a3),.e5(a5),.e7(a7),.prime_select(valuation_prime),
        .exponent(valuation),.valid(valuation_valid),.infinite(valuation_infinite));

    always @* begin
        assume(a2<=T2 && a3<=T3 && a5<=T5 && a7<=T7);
        assume(b2<=T2 && b3<=T3 && b5<=T5 && b7<=T7);
        if (zero_a) assume(a2==0 && a3==0 && a5==0 && a7==0);
        if (zero_b) assume(b2==0 && b3==0 && b5==0 && b7==0);
        assert(cz == (zero_a||zero_b));
        if (cz) begin
            assert(c2==0 && c3==0 && c5==0 && c7==0 && saturated==0);
        end else begin
            assert(c2 == ((sum2>T2)?T2:sum2));
            assert(c3 == ((sum3>T3)?T3:sum3));
            assert(c5 == ((sum5>T5)?T5:sum5));
            assert(c7 == ((sum7>T7)?T7:sum7));
            assert(saturated == {sum7>T7,sum5>T5,sum3>T3,sum2>T2});
        end
        assert(exact == !invalid);
        assert(xz == zero_a);
        if (invalid) begin
            assert(x2==a2 && x3==a3 && x5==a5 && x7==a7);
        end else if (zero_a) begin
            assert(x2==0 && x3==0 && x5==0 && x7==0);
        end else begin
            assert(x2==a2-b2 && x3==a3-b3 && x5==a5-b5 && x7==a7-b7);
        end
        assert(mz == (zero_a&&zero_b));
        if (zero_a) begin assert(m2==b2 && m3==b3 && m5==b5 && m7==b7); end
        else if (zero_b) begin assert(m2==a2 && m3==a3 && m5==a5 && m7==a7); end
        else begin
            assert(m2==((a2<b2)?a2:b2) && m3==((a3<b3)?a3:b3));
            assert(m5==((a5<b5)?a5:b5) && m7==((a7<b7)?a7:b7));
        end
        assert(jz == (zero_a||zero_b));
        if (jz) begin assert(j2==0 && j3==0 && j5==0 && j7==0); end
        else begin
            assert(j2==((a2>b2)?a2:b2) && j3==((a3>b3)?a3:b3));
            assert(j5==((a5>b5)?a5:b5) && j7==((a7>b7)?a7:b7));
        end
        assert(divides == (zero_a ? zero_b : (zero_b ? 1'b1 : ((a2<=b2)&&(a3<=b3)&&(a5<=b5)&&(a7<=b7)))));
        assert(valuation_valid);
        assert(valuation_infinite==zero_a);
        assert(valuation==(zero_a?0:a5));
    end
endmodule

module formal_binexp_w4(input wire zero_a,input wire zero_b,input wire [1:0] a2,input wire [1:0] b2,input wire [1:0] a3,input wire [1:0] b3,input wire a5,input wire b5,input wire a7,input wire b7); formal_binexp_core #(.E2W(2),.T2(3),.E3W(2),.T3(2),.E5W(1),.T5(1),.E7W(1),.T7(1)) proof(.*); endmodule
module formal_binexp_w6(input wire zero_a,input wire zero_b,input wire [2:0] a2,input wire [2:0] b2,input wire [1:0] a3,input wire [1:0] b3,input wire [1:0] a5,input wire [1:0] b5,input wire [1:0] a7,input wire [1:0] b7); formal_binexp_core #(.E2W(3),.T2(5),.E3W(2),.T3(3),.E5W(2),.T5(2),.E7W(2),.T7(2)) proof(.*); endmodule
module formal_binexp_w8(input wire zero_a,input wire zero_b,input wire [2:0] a2,input wire [2:0] b2,input wire [2:0] a3,input wire [2:0] b3,input wire [1:0] a5,input wire [1:0] b5,input wire [1:0] a7,input wire [1:0] b7); formal_binexp_core #(.E2W(3),.T2(7),.E3W(3),.T3(5),.E5W(2),.T5(3),.E7W(2),.T7(2)) proof(.*); endmodule

`default_nettype wire
