`timescale 1ns/1ps
`default_nettype none

module tb_binary #(
    parameter integer W = 4
);
    localparam integer LIMIT = (1 << W);
    localparam integer MASK = LIMIT - 1;
    reg [W-1:0] a;
    reg [W-1:0] b;
    reg [1:0] opcode;
    wire [W-1:0] add_y;
    wire [W-1:0] sub_y;
    wire add_carry;
    wire add_borrow_unused;
    wire sub_carry_unused;
    wire sub_borrow;
    wire equal;
    wire less_than;
    wire [2*W-1:0] product;
    wire [2*W-1:0] fu_result;
    wire [3:0] fu_status;
    integer ai, bi, op;
    integer sum_expected;
    integer product_expected;
    reg [2*W-1:0] result_expected;
    reg [3:0] status_expected;

    pa_addsub #(.W(W)) u_add(.a(a), .b(b), .subtract(1'b0), .y(add_y), .carry_out(add_carry), .borrow_out(add_borrow_unused));
    pa_addsub #(.W(W)) u_sub(.a(a), .b(b), .subtract(1'b1), .y(sub_y), .carry_out(sub_carry_unused), .borrow_out(sub_borrow));
    pa_unsigned_compare #(.W(W)) u_cmp(.a(a), .b(b), .equal(equal), .less_than(less_than));
    pa_shift_add_multiplier #(.W(W)) u_mul(.a(a), .b(b), .product(product));
    pa_bin_fu #(.W(W)) u_fu(.a(a), .b(b), .opcode(opcode), .result(fu_result), .status(fu_status));

    initial begin
        opcode = 0;
        for (ai = 0; ai < LIMIT; ai = ai + 1) begin
            for (bi = 0; bi < LIMIT; bi = bi + 1) begin
                a = ai[W-1:0];
                b = bi[W-1:0];
                #1;
                sum_expected = ai + bi;
                product_expected = ai * bi;
                if (add_y !== (sum_expected & MASK)) $fatal(1, "W=%0d ADD value a=%0d b=%0d", W, ai, bi);
                if (add_carry !== (sum_expected >= LIMIT)) $fatal(1, "W=%0d ADD carry a=%0d b=%0d", W, ai, bi);
                if (sub_y !== ((ai - bi) & MASK)) $fatal(1, "W=%0d SUB value a=%0d b=%0d", W, ai, bi);
                if (sub_borrow !== (ai < bi)) $fatal(1, "W=%0d SUB borrow a=%0d b=%0d", W, ai, bi);
                if (equal !== (ai == bi)) $fatal(1, "W=%0d EQ a=%0d b=%0d", W, ai, bi);
                if (less_than !== (ai < bi)) $fatal(1, "W=%0d LT a=%0d b=%0d", W, ai, bi);
                if (product !== product_expected[2*W-1:0]) $fatal(1, "W=%0d MUL a=%0d b=%0d", W, ai, bi);
                status_expected = {(product_expected >> W) != 0, ai == bi, ai < bi, sum_expected >= LIMIT};
                for (op = 0; op < 4; op = op + 1) begin
                    opcode = op[1:0];
                    case (op)
                        0: result_expected = {{W{1'b0}}, ((ai + bi) & MASK)};
                        1: result_expected = {{W{1'b0}}, ((ai - bi) & MASK)};
                        2: result_expected = product_expected;
                        default: result_expected = {{(2*W-2){1'b0}}, (ai < bi), (ai == bi)};
                    endcase
                    #1;
                    if (fu_result !== result_expected) $fatal(1, "W=%0d FU result op=%0d a=%0d b=%0d", W, op, ai, bi);
                    if (fu_status !== status_expected) $fatal(1, "W=%0d FU status op=%0d a=%0d b=%0d", W, op, ai, bi);
                end
            end
        end
        $display("PASS tb_binary W=%0d pairs=%0d", W, LIMIT*LIMIT);
        $finish;
    end
endmodule

`default_nettype wire
