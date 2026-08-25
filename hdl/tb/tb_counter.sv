`timescale 1ns/1ps
`default_nettype none

module tb_counter #(
    parameter integer W = 4
);
    localparam integer LIMIT = (1 << W);
    reg clk = 0;
    reg reset = 1;
    reg enable = 0;
    wire [W-1:0] count;
    wire overflow;
    integer i;
    always #1 clk = ~clk;
    pa_binary_counter #(.W(W)) dut(.clk(clk), .reset(reset), .enable(enable), .count(count), .overflow(overflow));

    initial begin
        repeat (2) @(posedge clk);
        #0; reset = 0; enable = 1;
        for (i = 0; i < LIMIT; i = i + 1) begin
            #1;
            if (count !== i[W-1:0]) $fatal(1, "counter pre-state W=%0d i=%0d got=%0d", W, i, count);
            if (overflow !== (i == LIMIT-1)) $fatal(1, "counter overflow W=%0d i=%0d", W, i);
            @(posedge clk);
        end
        #1;
        if (count !== 0) $fatal(1, "counter wrap W=%0d got=%0d", W, count);
        enable = 0;
        @(posedge clk); #1;
        if (count !== 0) $fatal(1, "counter enable hold W=%0d", W);
        $display("PASS tb_counter W=%0d states=%0d", W, LIMIT);
        $finish;
    end
endmodule

`default_nettype wire
