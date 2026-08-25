`default_nettype none

module formal_binary_core #(
    parameter integer W = 4
)(
    input wire [W-1:0] a,
    input wire [W-1:0] b,
    input wire [1:0] opcode
);
    wire [W-1:0] add_y, sub_y;
    wire add_carry, add_borrow_unused, sub_carry_unused, sub_borrow;
    wire equal, less_than;
    wire [2*W-1:0] product;
    wire [2*W-1:0] fu_result;
    wire [3:0] fu_status;
    wire [W:0] add_expected = {1'b0,a} + {1'b0,b};
    wire [W:0] sub_expected = {1'b0,a} - {1'b0,b};
    wire [2*W-1:0] product_expected = a * b;

    pa_addsub #(.W(W)) add(.a(a),.b(b),.subtract(1'b0),.y(add_y),.carry_out(add_carry),.borrow_out(add_borrow_unused));
    pa_addsub #(.W(W)) sub(.a(a),.b(b),.subtract(1'b1),.y(sub_y),.carry_out(sub_carry_unused),.borrow_out(sub_borrow));
    pa_unsigned_compare #(.W(W)) cmp(.a(a),.b(b),.equal(equal),.less_than(less_than));
    pa_shift_add_multiplier #(.W(W)) mul(.a(a),.b(b),.product(product));
    pa_bin_fu #(.W(W)) fu(.a(a),.b(b),.opcode(opcode),.result(fu_result),.status(fu_status));

    always @* begin
        assert(add_y == add_expected[W-1:0]);
        assert(add_carry == add_expected[W]);
        assert(sub_y == sub_expected[W-1:0]);
        assert(sub_borrow == (a < b));
        assert(equal == (a == b));
        assert(less_than == (a < b));
        assert(product == product_expected);
        case (opcode)
            2'b00: assert(fu_result == {{W{1'b0}}, add_expected[W-1:0]});
            2'b01: assert(fu_result == {{W{1'b0}}, sub_expected[W-1:0]});
            2'b10: assert(fu_result == product_expected);
            2'b11: assert(fu_result == {{(2*W-2){1'b0}}, (a < b), (a == b)});
        endcase
        assert(fu_status == {|product_expected[2*W-1:W], (a == b), (a < b), add_expected[W]});
    end
endmodule

module formal_binary_w4(input wire [3:0] a,input wire [3:0] b,input wire [1:0] opcode); formal_binary_core #(.W(4)) proof(.*); endmodule
module formal_binary_w6(input wire [5:0] a,input wire [5:0] b,input wire [1:0] opcode); formal_binary_core #(.W(6)) proof(.*); endmodule
module formal_binary_w8(input wire [7:0] a,input wire [7:0] b,input wire [1:0] opcode); formal_binary_core #(.W(8)) proof(.*); endmodule

`default_nettype wire
