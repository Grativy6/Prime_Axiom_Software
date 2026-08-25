`default_nettype none

module formal_therm_core #(
    parameter integer E2W=2, parameter integer T2=3,
    parameter integer E3W=2, parameter integer T3=2,
    parameter integer E5W=1, parameter integer T5=1,
    parameter integer E7W=1, parameter integer T7=1
)(
    input wire zero_a,input wire zero_b,
    input wire [T2-1:0] a2,input wire [T2-1:0] b2,
    input wire [T3-1:0] a3,input wire [T3-1:0] b3,
    input wire [T5-1:0] a5,input wire [T5-1:0] b5,
    input wire [T7-1:0] a7,input wire [T7-1:0] b7
);
    wire valid_a, valid_b;
    wire cz,mz,jz;
    wire [T2-1:0] c2,m2,j2;
    wire [T3-1:0] c3,m3,j3;
    wire [T5-1:0] c5,m5,j5;
    wire [T7-1:0] c7,m7,j7;
    wire [3:0] saturated;
    wire divides;
    wire [E2W-1:0] ea2,eb2;
    wire [E3W-1:0] ea3,eb3;
    wire [E5W-1:0] ea5,eb5;
    wire [E7W-1:0] ea7,eb7;
    wire decode_valid_a, decode_valid_b;
    wire [T2-1:0] round2;
    wire [T3-1:0] round3;
    wire [T5-1:0] round5;
    wire [T7-1:0] round7;
    wire encode_valid;

    pa_therm_validate_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) va(.t2(a2),.t3(a3),.t5(a5),.t7(a7),.valid(valid_a));
    pa_therm_validate_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) vb(.t2(b2),.t3(b3),.t5(b5),.t7(b7),.valid(valid_b));
    pa_therm_compose_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) compose(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.zero_y(cz),.y2(c2),.y3(c3),.y5(c5),.y7(c7),.saturated(saturated));
    pa_therm_meet_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) meet(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.zero_y(mz),.y2(m2),.y3(m3),.y5(m5),.y7(m7));
    pa_therm_join_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) u_join(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.zero_y(jz),.y2(j2),.y3(j3),.y5(j5),.y7(j7));
    pa_therm_divides_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) divq(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.divides(divides));
    pa_s4_therm_to_binexp #(.E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),.E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)) da(
        .t2(a2),.t3(a3),.t5(a5),.t7(a7),.e2(ea2),.e3(ea3),.e5(ea5),.e7(ea7),.valid(decode_valid_a));
    pa_s4_therm_to_binexp #(.E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),.E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)) db(
        .t2(b2),.t3(b3),.t5(b5),.t7(b7),.e2(eb2),.e3(eb3),.e5(eb5),.e7(eb7),.valid(decode_valid_b));
    pa_s4_binexp_to_therm #(.E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),.E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)) roundtrip(
        .e2(ea2),.e3(ea3),.e5(ea5),.e7(ea7),.t2(round2),.t3(round3),.t5(round5),.t7(round7),.valid(encode_valid));

    always @* begin
        // Validator is unconstrained until these equivalence assertions fire.
        assert(valid_a == decode_valid_a);
        assert(valid_b == decode_valid_b);
        if (valid_a) begin
            assert(encode_valid);
            assert(round2==a2 && round3==a3 && round5==a5 && round7==a7);
        end
        assume(valid_a && valid_b);
        if (zero_a) assume(a2==0 && a3==0 && a5==0 && a7==0);
        if (zero_b) assume(b2==0 && b3==0 && b5==0 && b7==0);

        assert(cz==(zero_a||zero_b));
        if (cz) begin
            assert(c2==0 && c3==0 && c5==0 && c7==0 && saturated==0);
        end else begin
            for (integer k2=0;k2<T2;k2=k2+1) assert(c2[k2] == ((ea2+eb2)>k2));
            for (integer k3=0;k3<T3;k3=k3+1) assert(c3[k3] == ((ea3+eb3)>k3));
            for (integer k5=0;k5<T5;k5=k5+1) assert(c5[k5] == ((ea5+eb5)>k5));
            for (integer k7=0;k7<T7;k7=k7+1) assert(c7[k7] == ((ea7+eb7)>k7));
            assert(saturated=={(ea7+eb7)>T7,(ea5+eb5)>T5,(ea3+eb3)>T3,(ea2+eb2)>T2});
        end
        assert(mz==(zero_a&&zero_b));
        if (zero_a) begin assert(m2==b2&&m3==b3&&m5==b5&&m7==b7); end
        else if (zero_b) begin assert(m2==a2&&m3==a3&&m5==a5&&m7==a7); end
        else begin assert(m2==(a2&b2)&&m3==(a3&b3)&&m5==(a5&b5)&&m7==(a7&b7)); end
        assert(jz==(zero_a||zero_b));
        if (jz) begin assert(j2==0&&j3==0&&j5==0&&j7==0); end
        else begin assert(j2==(a2|b2)&&j3==(a3|b3)&&j5==(a5|b5)&&j7==(a7|b7)); end
        assert(divides==(zero_a?zero_b:(zero_b?1'b1:((ea2<=eb2)&&(ea3<=eb3)&&(ea5<=eb5)&&(ea7<=eb7)))));
    end
endmodule

module formal_therm_w4(input wire zero_a,input wire zero_b,input wire [2:0] a2,input wire [2:0] b2,input wire [1:0] a3,input wire [1:0] b3,input wire a5,input wire b5,input wire a7,input wire b7); formal_therm_core #(.E2W(2),.T2(3),.E3W(2),.T3(2),.E5W(1),.T5(1),.E7W(1),.T7(1)) proof(.*); endmodule
module formal_therm_w6(input wire zero_a,input wire zero_b,input wire [4:0] a2,input wire [4:0] b2,input wire [2:0] a3,input wire [2:0] b3,input wire [1:0] a5,input wire [1:0] b5,input wire [1:0] a7,input wire [1:0] b7); formal_therm_core #(.E2W(3),.T2(5),.E3W(2),.T3(3),.E5W(2),.T5(2),.E7W(2),.T7(2)) proof(.*); endmodule
module formal_therm_w8(input wire zero_a,input wire zero_b,input wire [6:0] a2,input wire [6:0] b2,input wire [4:0] a3,input wire [4:0] b3,input wire [2:0] a5,input wire [2:0] b5,input wire [1:0] a7,input wire [1:0] b7); formal_therm_core #(.E2W(3),.T2(7),.E3W(3),.T3(5),.E5W(2),.T5(3),.E7W(2),.T7(2)) proof(.*); endmodule

`default_nettype wire
