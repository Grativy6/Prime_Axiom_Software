`timescale 1ns/1ps
`default_nettype none

module tb_binexp #(
    parameter integer W = 4
);
    localparam integer E2W = (W == 4) ? 2 : 3;
    localparam integer T2  = (W == 4) ? 3 : ((W == 6) ? 5 : 7);
    localparam integer E3W = (W == 8) ? 3 : 2;
    localparam integer T3  = (W == 4) ? 2 : ((W == 6) ? 3 : 5);
    localparam integer E5W = (W == 4) ? 1 : 2;
    localparam integer T5  = (W == 4) ? 1 : ((W == 6) ? 2 : 3);
    localparam integer E7W = (W == 4) ? 1 : 2;
    localparam integer T7  = (W == 4) ? 1 : 2;
    localparam integer KW  = (W == 4) ? 2 : 3;
    localparam integer STATES = (T2+1)*(T3+1)*(T5+1)*(T7+1);

    reg zero_a, zero_b;
    reg [E2W-1:0] a2, b2;
    reg [E3W-1:0] a3, b3;
    reg [E5W-1:0] a5, b5;
    reg [E7W-1:0] a7, b7;
    wire compose_zero;
    wire [E2W-1:0] compose2;
    wire [E3W-1:0] compose3;
    wire [E5W-1:0] compose5;
    wire [E7W-1:0] compose7;
    wire [3:0] saturated;
    wire cancel_zero;
    wire [E2W-1:0] cancel2;
    wire [E3W-1:0] cancel3;
    wire [E5W-1:0] cancel5;
    wire [E7W-1:0] cancel7;
    wire cancel_exact;
    wire meet_zero, join_zero;
    wire [E2W-1:0] meet2, join2;
    wire [E3W-1:0] meet3, join3;
    wire [E5W-1:0] meet5, join5;
    wire [E7W-1:0] meet7, join7;
    wire divides;
    reg [1:0] prime_select;
    reg [KW-1:0] power_k;
    wire [KW-1:0] valuation;
    wire valuation_valid;
    wire valuation_infinite;
    wire [E2W-1:0] power2;
    wire [E3W-1:0] power3;
    wire [E5W-1:0] power5;
    wire [E7W-1:0] power7;
    wire power_valid;
    integer sa, sb, ta, tb, p, k;
    integer ea2, ea3, ea5, ea7, eb2, eb3, eb5, eb7;
    integer expected_exact;

    pa_binexp_compose_s4 #(.E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),.E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)) compose(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .zero_y(compose_zero),.y2(compose2),.y3(compose3),.y5(compose5),.y7(compose7),.saturated(saturated));
    pa_binexp_cancel_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W)) cancel(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .zero_y(cancel_zero),.y2(cancel2),.y3(cancel3),.y5(cancel5),.y7(cancel7),.exact(cancel_exact));
    pa_binexp_meet_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W)) meet(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .zero_y(meet_zero),.y2(meet2),.y3(meet3),.y5(meet5),.y7(meet7));
    pa_binexp_join_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W)) u_join(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .zero_y(join_zero),.y2(join2),.y3(join3),.y5(join5),.y7(join7));
    pa_binexp_divides_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W)) divq(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.divides(divides));
    pa_binexp_valuation_s4 #(.E2W(E2W),.E3W(E3W),.E5W(E5W),.E7W(E7W),.KW(KW)) valq(
        .zero(zero_a),.e2(a2),.e3(a3),.e5(a5),.e7(a7),.prime_select(prime_select),.exponent(valuation),.valid(valuation_valid),.infinite(valuation_infinite));
    pa_binexp_power_s4 #(.E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),.E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7),.KW(KW)) powerq(
        .prime_select(prime_select),.exponent(power_k),.y2(power2),.y3(power3),.y5(power5),.y7(power7),.valid(power_valid));

    function automatic integer imin(input integer x, input integer y);
        imin = (x < y) ? x : y;
    endfunction
    function automatic integer imax(input integer x, input integer y);
        imax = (x > y) ? x : y;
    endfunction

    initial begin
        zero_a = 0; zero_b = 0; prime_select = 0; power_k = 0;
        for (sa = 0; sa < STATES; sa = sa + 1) begin
            ta = sa;
            ea2 = ta % (T2+1); ta = ta / (T2+1);
            ea3 = ta % (T3+1); ta = ta / (T3+1);
            ea5 = ta % (T5+1); ta = ta / (T5+1);
            ea7 = ta % (T7+1);
            a2 = ea2; a3 = ea3; a5 = ea5; a7 = ea7;
            for (sb = 0; sb < STATES; sb = sb + 1) begin
                tb = sb;
                eb2 = tb % (T2+1); tb = tb / (T2+1);
                eb3 = tb % (T3+1); tb = tb / (T3+1);
                eb5 = tb % (T5+1); tb = tb / (T5+1);
                eb7 = tb % (T7+1);
                b2 = eb2; b3 = eb3; b5 = eb5; b7 = eb7;
                #1;
                if (compose_zero !== 0) $fatal(1, "binexp compose zero W=%0d", W);
                if (compose2 !== imin(ea2+eb2,T2) || compose3 !== imin(ea3+eb3,T3) || compose5 !== imin(ea5+eb5,T5) || compose7 !== imin(ea7+eb7,T7))
                    $fatal(1, "binexp compose value W=%0d sa=%0d sb=%0d", W, sa, sb);
                if (saturated !== {(ea7+eb7>T7),(ea5+eb5>T5),(ea3+eb3>T3),(ea2+eb2>T2)})
                    $fatal(1, "binexp compose saturation W=%0d sa=%0d sb=%0d", W, sa, sb);
                expected_exact = (ea2>=eb2)&&(ea3>=eb3)&&(ea5>=eb5)&&(ea7>=eb7);
                if (cancel_exact !== expected_exact) $fatal(1, "binexp cancel exact W=%0d sa=%0d sb=%0d", W, sa, sb);
                if (expected_exact) begin
                    if (cancel2 !== ea2-eb2 || cancel3 !== ea3-eb3 || cancel5 !== ea5-eb5 || cancel7 !== ea7-eb7)
                        $fatal(1, "binexp cancel value W=%0d sa=%0d sb=%0d", W, sa, sb);
                end else if (cancel2 !== a2 || cancel3 !== a3 || cancel5 !== a5 || cancel7 !== a7) begin
                    $fatal(1, "binexp cancel atomic W=%0d sa=%0d sb=%0d", W, sa, sb);
                end
                if (meet2 !== imin(ea2,eb2) || meet3 !== imin(ea3,eb3) || meet5 !== imin(ea5,eb5) || meet7 !== imin(ea7,eb7))
                    $fatal(1, "binexp meet W=%0d sa=%0d sb=%0d", W, sa, sb);
                if (join2 !== imax(ea2,eb2) || join3 !== imax(ea3,eb3) || join5 !== imax(ea5,eb5) || join7 !== imax(ea7,eb7))
                    $fatal(1, "binexp join W=%0d sa=%0d sb=%0d", W, sa, sb);
                if (divides !== ((ea2<=eb2)&&(ea3<=eb3)&&(ea5<=eb5)&&(ea7<=eb7)))
                    $fatal(1, "binexp divides W=%0d sa=%0d sb=%0d", W, sa, sb);
            end
            for (p = 0; p < 4; p = p + 1) begin
                prime_select = p[1:0]; #1;
                case (p)
                    0: if (valuation !== ea2) $fatal(1, "valuation p2 W=%0d", W);
                    1: if (valuation !== ea3) $fatal(1, "valuation p3 W=%0d", W);
                    2: if (valuation !== ea5) $fatal(1, "valuation p5 W=%0d", W);
                    3: if (valuation !== ea7) $fatal(1, "valuation p7 W=%0d", W);
                endcase
                if (!valuation_valid) $fatal(1, "valuation valid W=%0d", W);
                if (valuation_infinite) $fatal(1, "finite valuation marked infinite W=%0d", W);
            end
        end

        // POWER constructor, including every invalid code in the K-bit port.
        for (p = 0; p < 4; p = p + 1) begin
            prime_select = p[1:0];
            for (k = 0; k < (1 << KW); k = k + 1) begin
                power_k = k[KW-1:0]; #1;
                case (p)
                    0: begin
                        if (power_valid !== (k<=T2) || power2 !== ((k<=T2)?k:0) || power3!==0 || power5!==0 || power7!==0) $fatal(1,"power p2 W=%0d k=%0d",W,k);
                    end
                    1: begin
                        if (power_valid !== (k<=T3) || power3 !== ((k<=T3)?k:0) || power2!==0 || power5!==0 || power7!==0) $fatal(1,"power p3 W=%0d k=%0d",W,k);
                    end
                    2: begin
                        if (power_valid !== (k<=T5) || power5 !== ((k<=T5)?k:0) || power2!==0 || power3!==0 || power7!==0) $fatal(1,"power p5 W=%0d k=%0d",W,k);
                    end
                    3: begin
                        if (power_valid !== (k<=T7) || power7 !== ((k<=T7)?k:0) || power2!==0 || power3!==0 || power5!==0) $fatal(1,"power p7 W=%0d k=%0d",W,k);
                    end
                endcase
            end
        end

        // Explicit zero laws and rejection boundaries.
        a2=0; a3=0; a5=0; a7=0; b2=1; b3=0; b5=0; b7=0;
        zero_a=1; zero_b=0; #1;
        if (!compose_zero || compose2!==0 || saturated!==0) $fatal(1,"zero compose W=%0d",W);
        if (!cancel_zero || !cancel_exact || cancel2!==0) $fatal(1,"zero cancel W=%0d",W);
        if (divides) $fatal(1,"zero must not divide nonzero W=%0d",W);
        if (!valuation_valid || !valuation_infinite || valuation!==0) $fatal(1,"v_p(0) infinity contract W=%0d",W);
        zero_a=0; zero_b=1; a2=1; #1;
        if (cancel_exact || cancel2!==a2 || cancel_zero!==zero_a) $fatal(1,"divide by zero atomic W=%0d",W);
        if (!divides) $fatal(1,"nonzero must divide zero W=%0d",W);
        zero_a=1; zero_b=1; #1;
        if (!divides) $fatal(1,"zero divides zero under existential convention W=%0d",W);

        $display("PASS tb_binexp W=%0d legal_states=%0d pairs=%0d", W, STATES, STATES*STATES);
        $finish;
    end
endmodule

`default_nettype wire
