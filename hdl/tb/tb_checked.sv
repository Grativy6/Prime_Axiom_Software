`timescale 1ns/1ps
`default_nettype none

module tb_checked #(
    parameter integer W = 4
);
    localparam integer E2W=(W==4)?2:3;
    localparam integer T2=(W==4)?3:((W==6)?5:7);
    localparam integer E3W=(W==8)?3:2;
    localparam integer T3=(W==4)?2:((W==6)?3:5);
    localparam integer E5W=(W==4)?1:2;
    localparam integer T5=(W==4)?1:((W==6)?2:3);
    localparam integer E7W=(W==4)?1:2;
    localparam integer T7=(W==4)?1:2;

    reg zero_a,zero_b,zero_c;
    reg [E2W-1:0] a2,b2,c2;
    reg [E3W-1:0] a3,b3,c3;
    reg [E5W-1:0] a5,b5,c5;
    reg [E7W-1:0] a7,b7,c7;
    reg [3:0] bad_a,bad_b,bad_c;
    wire zero_mid,zero_y;
    wire [E2W-1:0] mid2,y2;
    wire [E3W-1:0] mid3,y3;
    wire [E5W-1:0] mid5,y5;
    wire [E7W-1:0] mid7,y7;
    wire [3:0] bad_mid,bad_y;
    integer prior;

    pa_binexp_checked_compose_s4 #(
        .E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),
        .E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)
    ) first(
        .zero_a(zero_a),.zero_b(zero_b),
        .a2(a2),.a3(a3),.a5(a5),.a7(a7),
        .b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .bad_a(bad_a),.bad_b(bad_b),
        .zero_y(zero_mid),.y2(mid2),.y3(mid3),.y5(mid5),.y7(mid7),.bad_y(bad_mid)
    );
    pa_binexp_checked_compose_s4 #(
        .E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),
        .E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)
    ) second(
        .zero_a(zero_mid),.zero_b(zero_c),
        .a2(mid2),.a3(mid3),.a5(mid5),.a7(mid7),
        .b2(c2),.b3(c3),.b5(c5),.b7(c7),
        .bad_a(bad_mid),.bad_b(bad_c),
        .zero_y(zero_y),.y2(y2),.y3(y3),.y5(y5),.y7(y7),.bad_y(bad_y)
    );

    initial begin
        zero_a=0;zero_b=0;zero_c=0;
        a2=0;a3=0;a5=0;a7=0;
        b2=0;b3=0;b5=0;b7=0;
        c2=0;c3=0;c5=0;c7=0;
        bad_a=0;bad_b=0;bad_c=0;

        // Every possible incoming bad-tag pattern remains set across two
        // otherwise exact compose operations.
        for(prior=0;prior<16;prior=prior+1) begin
            bad_a=prior[3:0];#1;
            if(bad_mid!==prior[3:0]||bad_y!==prior[3:0])
                $fatal(1,"checked prior tag lost W=%0d prior=%0h",W,prior);
        end

        // Fresh saturation in all four lanes is sticky at re-entry.
        bad_a=0;bad_b=0;bad_c=0;
        a2=T2;a3=T3;a5=T5;a7=T7;
        b2=1;b3=1;b5=1;b7=1;#1;
        if(bad_mid!==4'hf||bad_y!==4'hf)
            $fatal(1,"checked saturation re-entered exact W=%0d mid=%0h y=%0h",W,bad_mid,bad_y);
        if(y2!==T2||y3!==T3||y5!==T5||y7!==T7)
            $fatal(1,"checked clamp W=%0d",W);

        // The explicit zero tag earns a fresh exact zero state and clears lane
        // payload uncertainty at both stages.
        zero_a=1;a2=0;a3=0;a5=0;a7=0;bad_a=4'hf;#1;
        if(!zero_mid||!zero_y||bad_mid!==0||bad_y!==0||y2!==0||y3!==0||y5!==0||y7!==0)
            $fatal(1,"checked exact zero W=%0d",W);

        $display("PASS tb_checked W=%0d persistent_bad_patterns=16 two_stage_reentry=1",W);
        $finish;
    end
endmodule

`default_nettype wire
