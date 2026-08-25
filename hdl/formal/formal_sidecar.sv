`default_nettype none

module formal_sidecar_core #(
    parameter integer W=4,
    parameter integer T2=3,parameter integer T3=2,
    parameter integer T5=1,parameter integer T7=1
)(
    input wire [W-1:0] magnitude,
    input wire [1:0] operation,
    input wire [1:0] prime_select
);
    localparam integer MAX_VALUE=(1<<W)-1;
    wire zero,valid;
    wire [T2-1:0] t2;wire [T3-1:0] t3;wire [T5-1:0] t5;wire [T7-1:0] t7;
    wire [W-1:0] magnitude_y;
    wire valid_y;
    wire [T2-1:0] y2;wire [T3-1:0] y3;wire [T5-1:0] y5;wire [T7-1:0] y7;
    wire predicate,rejected,overflow;
    wire expected_zero,expected_valid;
    wire [T2-1:0] expected2;wire [T3-1:0] expected3;wire [T5-1:0] expected5;wire [T7-1:0] expected7;
    reg [W-1:0] factor;
    wire [2*W-1:0] wide_product=magnitude*factor;

    pa_cold_encode_s4 #(.W(W),.T2(T2),.T3(T3),.T5(T5),.T7(T7)) enc(
        .magnitude(magnitude),.zero(zero),.valid(valid),.t2(t2),.t3(t3),.t5(t5),.t7(t7));
    pa_bin_vsc_s4 #(.W(W),.T2(T2),.T3(T3),.T5(T5),.T7(T7)) dut(
        .magnitude(magnitude),.sidecar_valid(valid),.t2(t2),.t3(t3),.t5(t5),.t7(t7),.operation(operation),.prime_select(prime_select),
        .magnitude_y(magnitude_y),.valid_y(valid_y),.y2(y2),.y3(y3),.y5(y5),.y7(y7),.predicate(predicate),.rejected(rejected),.overflow(overflow));
    pa_cold_encode_s4 #(.W(W),.T2(T2),.T3(T3),.T5(T5),.T7(T7)) expected_enc(
        .magnitude(magnitude_y),.zero(expected_zero),.valid(expected_valid),.t2(expected2),.t3(expected3),.t5(expected5),.t7(expected7));

    function automatic integer ipow(input integer base,input integer exponent);
        integer value;
        begin value=1;for(integer i=0;i<exponent;i=i+1)value=value*base;ipow=value;end
    endfunction

    always @* begin
        case(prime_select)
            0:factor=2;
            1:factor=3;
            2:factor=5;
            default:factor=7;
        endcase
        assert(zero==(magnitude==0));
        assert(valid);
        for(integer k2=0;k2<T2;k2=k2+1) assert(t2[k2]==((magnitude==0)||((magnitude%ipow(2,k2+1))==0)));
        for(integer k3=0;k3<T3;k3=k3+1) assert(t3[k3]==((magnitude==0)||((magnitude%ipow(3,k3+1))==0)));
        for(integer k5=0;k5<T5;k5=k5+1) assert(t5[k5]==((magnitude==0)||((magnitude%ipow(5,k5+1))==0)));
        for(integer k7=0;k7<T7;k7=k7+1) assert(t7[k7]==((magnitude==0)||((magnitude%ipow(7,k7+1))==0)));
        case(operation)
            0:begin
                assert(magnitude_y==magnitude&&valid_y==valid&&y2==t2&&y3==t3&&y5==t5&&y7==t7);
                assert(!rejected);
                assert(predicate==((magnitude==0)||((magnitude%factor)==0)));
                assert(!overflow);
            end
            1:begin
                assert(overflow==(wide_product>MAX_VALUE));
                if(wide_product>MAX_VALUE) begin
                    assert(rejected&&magnitude_y==magnitude&&valid_y==valid&&y2==t2&&y3==t3&&y5==t5&&y7==t7);
                end else begin
                    assert(!rejected&&magnitude_y==wide_product[W-1:0]&&valid_y);
                    assert(y2==expected2&&y3==expected3&&y5==expected5&&y7==expected7);
                end
                assert(!predicate);
            end
            2:begin
                assert(!overflow&&!predicate);
                if((magnitude==0)||((magnitude%factor)==0)) begin
                    assert(!rejected&&magnitude_y==((magnitude==0)?0:(magnitude/factor))&&valid_y);
                    assert(y2==expected2&&y3==expected3&&y5==expected5&&y7==expected7);
                end else begin
                    assert(rejected&&magnitude_y==magnitude&&valid_y==valid&&y2==t2&&y3==t3&&y5==t5&&y7==t7);
                end
            end
            default:begin
                assert(!rejected&&!overflow&&!predicate);
                assert(magnitude_y==magnitude&&!valid_y&&y2==t2&&y3==t3&&y5==t5&&y7==t7);
            end
        endcase
    end
endmodule

module formal_sidecar_w4(input wire [3:0] magnitude,input wire [1:0] operation,input wire [1:0] prime_select);formal_sidecar_core #(.W(4),.T2(3),.T3(2),.T5(1),.T7(1)) proof(.*);endmodule
module formal_sidecar_w6(input wire [5:0] magnitude,input wire [1:0] operation,input wire [1:0] prime_select);formal_sidecar_core #(.W(6),.T2(5),.T3(3),.T5(2),.T7(2)) proof(.*);endmodule
module formal_sidecar_w8(input wire [7:0] magnitude,input wire [1:0] operation,input wire [1:0] prime_select);formal_sidecar_core #(.W(8),.T2(7),.T3(5),.T5(3),.T7(2)) proof(.*);endmodule

`default_nettype wire
