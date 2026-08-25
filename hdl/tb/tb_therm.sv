`timescale 1ns/1ps
`default_nettype none

module tb_therm #(
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
    localparam integer STATES = (T2+1)*(T3+1)*(T5+1)*(T7+1);

    reg zero_a, zero_b;
    reg [T2-1:0] a2, b2;
    reg [T3-1:0] a3, b3;
    reg [T5-1:0] a5, b5;
    reg [T7-1:0] a7, b7;
    reg [E2W-1:0] ae2;
    reg [E3W-1:0] ae3;
    reg [E5W-1:0] ae5;
    reg [E7W-1:0] ae7;
    wire compose_zero, meet_zero, join_zero;
    wire [T2-1:0] compose2, meet2, join2;
    wire [T3-1:0] compose3, meet3, join3;
    wire [T5-1:0] compose5, meet5, join5;
    wire [T7-1:0] compose7, meet7, join7;
    wire [3:0] saturated;
    wire divides;
    wire thermometer_valid;
    wire [T2-1:0] converted2;
    wire [T3-1:0] converted3;
    wire [T5-1:0] converted5;
    wire [T7-1:0] converted7;
    wire bin_to_therm_valid;
    wire [E2W-1:0] round2;
    wire [E3W-1:0] round3;
    wire [E5W-1:0] round5;
    wire [E7W-1:0] round7;
    wire therm_to_bin_valid;
    integer sa, sb, ta, tb;
    integer ea2, ea3, ea5, ea7, eb2, eb3, eb5, eb7;
    integer expected_divides;

    pa_therm_compose_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) compose(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .zero_y(compose_zero),.y2(compose2),.y3(compose3),.y5(compose5),.y7(compose7),.saturated(saturated));
    pa_therm_meet_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) meet(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .zero_y(meet_zero),.y2(meet2),.y3(meet3),.y5(meet5),.y7(meet7));
    pa_therm_join_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) u_join(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .zero_y(join_zero),.y2(join2),.y3(join3),.y5(join5),.y7(join7));
    pa_therm_divides_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) divq(
        .zero_a(zero_a),.zero_b(zero_b),.a2(a2),.a3(a3),.a5(a5),.a7(a7),.b2(b2),.b3(b3),.b5(b5),.b7(b7),.divides(divides));
    pa_therm_validate_s4 #(.T2(T2),.T3(T3),.T5(T5),.T7(T7)) validator(.t2(a2),.t3(a3),.t5(a5),.t7(a7),.valid(thermometer_valid));
    pa_s4_binexp_to_therm #(.E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),.E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)) to_therm(
        .e2(ae2),.e3(ae3),.e5(ae5),.e7(ae7),.t2(converted2),.t3(converted3),.t5(converted5),.t7(converted7),.valid(bin_to_therm_valid));
    pa_s4_therm_to_binexp #(.E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),.E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)) to_bin(
        .t2(a2),.t3(a3),.t5(a5),.t7(a7),.e2(round2),.e3(round3),.e5(round5),.e7(round7),.valid(therm_to_bin_valid));

    function automatic integer mask_for(input integer exponent);
        mask_for = (1 << exponent) - 1;
    endfunction
    function automatic integer imin(input integer x, input integer y);
        imin = (x < y) ? x : y;
    endfunction
    function automatic integer imax(input integer x, input integer y);
        imax = (x > y) ? x : y;
    endfunction

    initial begin
        zero_a=0; zero_b=0; ae2=0; ae3=0; ae5=0; ae7=0;
        for (sa = 0; sa < STATES; sa = sa + 1) begin
            ta = sa;
            ea2 = ta % (T2+1); ta = ta / (T2+1);
            ea3 = ta % (T3+1); ta = ta / (T3+1);
            ea5 = ta % (T5+1); ta = ta / (T5+1);
            ea7 = ta % (T7+1);
            a2=mask_for(ea2); a3=mask_for(ea3); a5=mask_for(ea5); a7=mask_for(ea7);
            ae2=ea2; ae3=ea3; ae5=ea5; ae7=ea7;
            #1;
            if (!thermometer_valid || !therm_to_bin_valid || !bin_to_therm_valid) $fatal(1,"therm conversion validity W=%0d state=%0d",W,sa);
            if (round2!==ea2 || round3!==ea3 || round5!==ea5 || round7!==ea7) $fatal(1,"therm decode W=%0d state=%0d",W,sa);
            if (converted2!==a2 || converted3!==a3 || converted5!==a5 || converted7!==a7) $fatal(1,"therm encode W=%0d state=%0d",W,sa);
            for (sb = 0; sb < STATES; sb = sb + 1) begin
                tb = sb;
                eb2 = tb % (T2+1); tb = tb / (T2+1);
                eb3 = tb % (T3+1); tb = tb / (T3+1);
                eb5 = tb % (T5+1); tb = tb / (T5+1);
                eb7 = tb % (T7+1);
                b2=mask_for(eb2); b3=mask_for(eb3); b5=mask_for(eb5); b7=mask_for(eb7);
                #1;
                if (compose2!==mask_for(imin(ea2+eb2,T2)) || compose3!==mask_for(imin(ea3+eb3,T3)) || compose5!==mask_for(imin(ea5+eb5,T5)) || compose7!==mask_for(imin(ea7+eb7,T7)))
                    $fatal(1,"therm compose W=%0d sa=%0d sb=%0d",W,sa,sb);
                if (saturated !== {(ea7+eb7>T7),(ea5+eb5>T5),(ea3+eb3>T3),(ea2+eb2>T2)}) $fatal(1,"therm saturation W=%0d sa=%0d sb=%0d",W,sa,sb);
                if (meet2!==mask_for(imin(ea2,eb2)) || meet3!==mask_for(imin(ea3,eb3)) || meet5!==mask_for(imin(ea5,eb5)) || meet7!==mask_for(imin(ea7,eb7))) $fatal(1,"therm meet W=%0d",W);
                if (join2!==mask_for(imax(ea2,eb2)) || join3!==mask_for(imax(ea3,eb3)) || join5!==mask_for(imax(ea5,eb5)) || join7!==mask_for(imax(ea7,eb7))) $fatal(1,"therm join W=%0d",W);
                expected_divides=(ea2<=eb2)&&(ea3<=eb3)&&(ea5<=eb5)&&(ea7<=eb7);
                if (divides!==expected_divides) $fatal(1,"therm divides W=%0d sa=%0d sb=%0d",W,sa,sb);
            end
        end

        // A higher threshold without its predecessor is malformed.
        a2 = {{(T2-2){1'b0}}, 2'b10}; a3=0; a5=0; a7=0; #1;
        if (thermometer_valid || therm_to_bin_valid) $fatal(1,"malformed thermometer accepted W=%0d",W);

        // Explicit zero laws.
        a2=0;a3=0;a5=0;a7=0;b2=1;b3=0;b5=0;b7=0;zero_a=1;zero_b=0;#1;
        if (!compose_zero || compose2!==0 || saturated!==0) $fatal(1,"therm zero compose W=%0d",W);
        if (divides) $fatal(1,"therm zero divides nonzero W=%0d",W);
        zero_a=0;zero_b=1;a2=1;#1;
        if (!divides) $fatal(1,"therm nonzero divides zero W=%0d",W);
        zero_a=1;zero_b=1;#1;
        if (!divides) $fatal(1,"therm zero divides zero W=%0d",W);

        $display("PASS tb_therm W=%0d legal_states=%0d pairs=%0d",W,STATES,STATES*STATES);
        $finish;
    end
endmodule

`default_nettype wire
