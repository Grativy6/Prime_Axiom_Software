`default_nettype none

module formal_checked_core #(
    parameter integer E2W=2,parameter integer T2=3,
    parameter integer E3W=2,parameter integer T3=2,
    parameter integer E5W=1,parameter integer T5=1,
    parameter integer E7W=1,parameter integer T7=1
)(
    input wire zero_a,input wire zero_b,
    input wire [E2W-1:0] a2,input wire [E2W-1:0] b2,
    input wire [E3W-1:0] a3,input wire [E3W-1:0] b3,
    input wire [E5W-1:0] a5,input wire [E5W-1:0] b5,
    input wire [E7W-1:0] a7,input wire [E7W-1:0] b7,
    input wire [3:0] bad_a,input wire [3:0] bad_b
);
    wire zero_y;
    wire [E2W-1:0] y2;
    wire [E3W-1:0] y3;
    wire [E5W-1:0] y5;
    wire [E7W-1:0] y7;
    wire [3:0] bad_y;
    wire [E2W:0] sum2={1'b0,a2}+{1'b0,b2};
    wire [E3W:0] sum3={1'b0,a3}+{1'b0,b3};
    wire [E5W:0] sum5={1'b0,a5}+{1'b0,b5};
    wire [E7W:0] sum7={1'b0,a7}+{1'b0,b7};
    wire [3:0] new_bad={sum7>T7,sum5>T5,sum3>T3,sum2>T2};
    wire [3:0] expected_bad=(zero_a||zero_b)?4'b0000:(bad_a|bad_b|new_bad);

    pa_binexp_checked_compose_s4 #(
        .E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),
        .E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)
    ) dut(
        .zero_a(zero_a),.zero_b(zero_b),
        .a2(a2),.a3(a3),.a5(a5),.a7(a7),
        .b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .bad_a(bad_a),.bad_b(bad_b),
        .zero_y(zero_y),.y2(y2),.y3(y3),.y5(y5),.y7(y7),.bad_y(bad_y)
    );

    always @* begin
        assume(a2<=T2&&a3<=T3&&a5<=T5&&a7<=T7);
        assume(b2<=T2&&b3<=T3&&b5<=T5&&b7<=T7);
        if(zero_a) assume(a2==0&&a3==0&&a5==0&&a7==0);
        if(zero_b) assume(b2==0&&b3==0&&b5==0&&b7==0);
        assert(zero_y==(zero_a||zero_b));
        assert(bad_y==expected_bad);
        if(zero_y) begin
            assert(y2==0&&y3==0&&y5==0&&y7==0);
        end else begin
            assert(y2==((sum2>T2)?T2:sum2));
            assert(y3==((sum3>T3)?T3:sum3));
            assert(y5==((sum5>T5)?T5:sum5));
            assert(y7==((sum7>T7)?T7:sum7));
        end
    end
endmodule

module formal_checked_w4(input wire zero_a,input wire zero_b,input wire [1:0] a2,input wire [1:0] b2,input wire [1:0] a3,input wire [1:0] b3,input wire a5,input wire b5,input wire a7,input wire b7,input wire [3:0] bad_a,input wire [3:0] bad_b);formal_checked_core #(.E2W(2),.T2(3),.E3W(2),.T3(2),.E5W(1),.T5(1),.E7W(1),.T7(1)) proof(.*);endmodule
module formal_checked_w6(input wire zero_a,input wire zero_b,input wire [2:0] a2,input wire [2:0] b2,input wire [1:0] a3,input wire [1:0] b3,input wire [1:0] a5,input wire [1:0] b5,input wire [1:0] a7,input wire [1:0] b7,input wire [3:0] bad_a,input wire [3:0] bad_b);formal_checked_core #(.E2W(3),.T2(5),.E3W(2),.T3(3),.E5W(2),.T5(2),.E7W(2),.T7(2)) proof(.*);endmodule
module formal_checked_w8(input wire zero_a,input wire zero_b,input wire [2:0] a2,input wire [2:0] b2,input wire [2:0] a3,input wire [2:0] b3,input wire [1:0] a5,input wire [1:0] b5,input wire [1:0] a7,input wire [1:0] b7,input wire [3:0] bad_a,input wire [3:0] bad_b);formal_checked_core #(.E2W(3),.T2(7),.E3W(3),.T3(5),.E5W(2),.T5(3),.E7W(2),.T7(2)) proof(.*);endmodule

`default_nettype wire
